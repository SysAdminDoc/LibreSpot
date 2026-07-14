using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using LibreSpot.Desktop.Models;
using Strings = LibreSpot.Desktop.Properties.Strings;

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
        var patcherOwnershipReport = BuildPatcherOwnershipReport(
            configDirectory,
            spicetifyConfigPath,
            spotifyInstalled,
            spicetifyInstalled);
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
            storeSpotifyPresent,
            patcherOwnershipReport);

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
            PatcherOwnershipReport = patcherOwnershipReport,
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
        bool storeSpotifyPresent,
        PatcherOwnershipReport patcherOwnershipReport)
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
        components.Add(BuildPatcherOwnershipComponent(patcherOwnershipReport));
        components.AddRange(BuildUpstreamDriftComponents(upstreamDriftReport));
        components.AddRange(BuildCommunityAssetComponents(communityAssetDriftReport));
        components.AddRange(BuildAntivirusComponents(antivirusStatus));
        components.AddRange(BuildStoreSpotifyComponents(storeSpotifyPresent));

        return new StackHealthReport(components);
    }

    private PatcherOwnershipReport BuildPatcherOwnershipReport(
        string configDirectory,
        string spicetifyConfigPath,
        bool spotifyInstalled,
        bool spicetifyInstalled)
    {
        var spotifyDirectory = Path.GetDirectoryName(_spotifyPath) ?? string.Empty;
        var appsDirectory = Path.Combine(spotifyDirectory, "Apps");
        var activeBundlePresent = File.Exists(Path.Combine(appsDirectory, "xpui.spa")) ||
                                  Directory.Exists(Path.Combine(appsDirectory, "xpui"));
        var spotXEvidence = new[]
            {
                Path.Combine(appsDirectory, "xpui.bak"),
                Path.Combine(appsDirectory, "xpui.spa.bak"),
                Path.Combine(spotifyDirectory, "Spotify.bak"),
                Path.Combine(spotifyDirectory, "chrome_elf.dll.bak")
            }
            .Where(File.Exists)
            .ToArray();
        var injectorEvidence = new[]
            {
                Path.Combine(spotifyDirectory, "dpapi.dll"),
                Path.Combine(spotifyDirectory, "config.ini"),
                Path.Combine(spotifyDirectory, "version.dll"),
                Path.Combine(spotifyDirectory, "winmm.dll")
            }
            .Where(File.Exists)
            .ToArray();
        var libreSpotEvidence = new[]
            {
                Path.Combine(configDirectory, "operation-journal.jsonl"),
                Path.Combine(configDirectory, "install.log"),
                Path.Combine(configDirectory, "spicetify-preservation-latest.json")
            }
            .Where(File.Exists)
            .ToArray();
        var footprints = new List<PatcherFootprint>();

        if (injectorEvidence.Length > 0)
        {
            footprints.Add(new PatcherFootprint(
                "likely-blockthespot",
                "Likely BlockTheSpot-family injector",
                "likely",
                PatcherOwnership.Foreign,
                injectorEvidence,
                "Create a Spicetify backup if applicable, then use Full Reset for a clean migration. LibreSpot will not remove these files outside an explicitly confirmed cleanup."));
        }

        var spotXPresent = spotifyInstalled && activeBundlePresent && spotXEvidence.Length > 0;
        var spicetifyPresent = spicetifyInstalled || File.Exists(spicetifyConfigPath);
        var libreSpotOwned = libreSpotEvidence.Length > 0;
        if (spotXPresent)
        {
            footprints.Add(new PatcherFootprint(
                libreSpotOwned ? "librespot-spotx" : "raw-spotx",
                libreSpotOwned ? "LibreSpot-managed SpotX" : "Raw SpotX",
                "verified",
                libreSpotOwned ? PatcherOwnership.LibreSpot : PatcherOwnership.Foreign,
                spotXEvidence,
                libreSpotOwned
                    ? "Continue with LibreSpot maintenance actions."
                    : "Keep the existing SpotX backups and use setup without Clean Install to adopt this state; choose Full Reset only when you intend to remove it."));
        }

        if (spicetifyPresent)
        {
            var evidence = new[] { _spicetifyPath, spicetifyConfigPath }
                .Where(File.Exists)
                .ToArray();
            footprints.Add(new PatcherFootprint(
                libreSpotOwned ? "librespot-spicetify" : "standalone-spicetify",
                libreSpotOwned ? "LibreSpot-managed Spicetify" : "Standalone Spicetify",
                "verified",
                libreSpotOwned ? PatcherOwnership.LibreSpot : PatcherOwnership.Foreign,
                evidence,
                libreSpotOwned
                    ? "Continue with LibreSpot maintenance actions."
                    : "Create a backup before setup. LibreSpot preserves the existing config and CustomApps state during migration."));
        }

        if (footprints.Count == 0)
        {
            return PatcherOwnershipReport.Empty;
        }

        var hasForeign = footprints.Any(footprint => footprint.Ownership == PatcherOwnership.Foreign);
        var hasLibreSpot = footprints.Any(footprint => footprint.Ownership == PatcherOwnership.LibreSpot);
        var ownership = hasForeign && hasLibreSpot
            ? PatcherOwnership.Mixed
            : hasForeign
                ? PatcherOwnership.Foreign
                : PatcherOwnership.LibreSpot;
        var foreignNames = footprints
            .Where(footprint => footprint.Ownership == PatcherOwnership.Foreign)
            .Select(footprint => footprint.Name)
            .ToArray();
        var summary = hasForeign
            ? $"Detected foreign customization state: {string.Join(", ", foreignNames)}."
            : "Detected only LibreSpot-managed customization state.";
        var recommendation = hasForeign
            ? string.Join(" ", footprints
                .Where(footprint => footprint.Ownership == PatcherOwnership.Foreign)
                .Select(footprint => footprint.Recommendation)
                .Distinct(StringComparer.Ordinal))
            : "Continue with LibreSpot maintenance actions.";

        return new PatcherOwnershipReport(ownership, summary, recommendation, footprints);
    }

    private static StackHealthComponent BuildPatcherOwnershipComponent(PatcherOwnershipReport report)
    {
        var severity = report.HasForeignState ? HealthSeverity.Warning : HealthSeverity.Ready;
        var status = report.Ownership switch
        {
            PatcherOwnership.Mixed => L("HealthStatusOwnershipMixed"),
            PatcherOwnership.Foreign => L("HealthStatusOwnershipForeign"),
            PatcherOwnership.LibreSpot => L("HealthStatusOwnershipLibreSpot"),
            _ => L("HealthStatusOwnershipNone")
        };
        var evidencePaths = report.Footprints.SelectMany(footprint => footprint.EvidencePaths).ToArray();
        var actions = report.HasForeignState && report.Footprints.Any(footprint =>
                footprint.Id.Contains("spicetify", StringComparison.OrdinalIgnoreCase))
            ? new[] { "CreateBackup" }
            : Array.Empty<string>();
        DateTime? lastChanged = evidencePaths.Length > 0
            ? evidencePaths.Select(File.GetLastWriteTime).Max()
            : null;

        return Component(
            "patcher-ownership",
            L("HealthNamePatcherOwnership"),
            status,
            severity,
            null,
            evidencePaths.FirstOrDefault(),
            lastChanged,
            F("HealthEvidencePatcherOwnershipFormat", report.Summary, report.Recommendation),
            actions);
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
            L("HealthNameStoreSpotify"),
            L("HealthStatusWillBeReplaced"),
            HealthSeverity.Info,
            null,
            null,
            null,
            L("HealthEvidenceStoreSpotify"),
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
            L("HealthNameAntivirusExclusion"),
            L("HealthStatusExclusionRecommended"),
            HealthSeverity.Warning,
            null,
            spotifyDirectory,
            null,
            F("HealthEvidenceAntivirusExclusionFormat", spotifyDirectory),
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
                        F("HealthEvidenceUpstreamUnavailableFormat", pin.PinnedValue, current, ex.Message));
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
                        F("HealthEvidenceCommunityUnavailableFormat", pin.PinnedCommit, ex.Message))),
                DateTimeOffset.UtcNow);
        }
    }

    private static IEnumerable<StackHealthComponent> BuildUpstreamDriftComponents(UpstreamDriftReport report)
    {
        foreach (var dependency in report.Dependencies)
        {
            var status = dependency.IsDegraded
                ? string.IsNullOrWhiteSpace(dependency.LatestValue) ? L("HealthStatusLatestUnknown") : L("HealthStatusCachedMetadata")
                : dependency.DriftState switch
                {
                    "current" => L("HealthStatusCurrent"),
                    "behind" => L("HealthStatusUpstreamChanged"),
                    "ahead" => L("HealthStatusPinnedAhead"),
                    _ => L("HealthStatusLatestUnknown")
                };
            var severity = string.Equals(dependency.DriftState, "current", StringComparison.OrdinalIgnoreCase) &&
                !dependency.IsDegraded
                    ? HealthSeverity.Ready
                    : HealthSeverity.Info;

            yield return Component(
                $"upstream-{dependency.Id}",
                F("HealthNameUpstreamFormat", dependency.Name),
                status,
                severity,
                dependency.LatestValue,
                null,
                dependency.CheckedAtUtc.LocalDateTime,
                F(
                    "HealthEvidenceUpstreamFormat",
                    dependency.PinnedValue,
                    dependency.CurrentValue,
                    dependency.LatestValue ?? L("HealthValueUnknown"),
                    dependency.DriftState,
                    dependency.MetadataSource,
                    FormatCacheAge(dependency.CacheAge)));
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
                F("HealthNameCommunityAssetFormat", asset.Name, asset.Kind),
                status,
                severity,
                asset.LatestCommit,
                asset.SourceUrl,
                asset.CheckedAtUtc.LocalDateTime,
                F(
                    "HealthEvidenceCommunityAssetFormat",
                    asset.PinnedCommit,
                    asset.LatestCommit ?? L("HealthValueUnknown"),
                    asset.DriftState,
                    asset.MetadataSource,
                    asset.License,
                    asset.NetworkBehavior),
                actions);
        }
    }

    private static string CommunityAssetStatus(CommunityAssetState asset)
    {
        if (string.Equals(asset.DriftState, "missing", StringComparison.OrdinalIgnoreCase))
        {
            return L("HealthStatusMissingUpstream");
        }

        if (asset.IsDegraded)
        {
            return string.IsNullOrWhiteSpace(asset.LatestCommit) ? L("HealthStatusLatestUnknown") : L("HealthStatusCachedMetadata");
        }

        if (string.Equals(asset.DriftState, "behind", StringComparison.OrdinalIgnoreCase))
        {
            return L("HealthStatusUpstreamChanged");
        }

        if (asset.RequiresTrustReview)
        {
            return L("HealthStatusReviewRequired");
        }

        return L("HealthStatusCurrent");
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
                L("HealthNameSpotify"),
                L("HealthStatusNotInstalled"),
                HealthSeverity.Info,
                null,
                _spotifyPath,
                null,
                L("HealthEvidenceSpotifyMissing"),
                "Install");
        }

        return Component(
            "spotify",
            L("HealthNameSpotify"),
            L("HealthStatusDetected"),
            HealthSeverity.Ready,
            _spotifyVersionProbe(),
            _spotifyPath,
            GetLastChanged(_spotifyPath),
            L("HealthEvidenceSpotifyDetected"));
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
                L("HealthNameSpotXPatch"),
                L("HealthStatusNotChecked"),
                HealthSeverity.Info,
                null,
                appsDirectory,
                null,
                L("HealthEvidenceSpotXNotChecked"),
                "Install");
        }

        if (hasBundle && (hasBackup || hasBinBackup))
        {
            return Component(
                "spotx",
                L("HealthNameSpotXPatch"),
                L("HealthStatusVerified"),
                HealthSeverity.Ready,
                null,
                appsDirectory,
                Max(GetLastChanged(bundlePath), Max(GetLastChanged(backupPath), GetLastChanged(spotifyBinBackup))),
                L("HealthEvidenceSpotXVerified"));
        }

        if (hasBundle)
        {
            return Component(
                "spotx",
                L("HealthNameSpotXPatch"),
                L("HealthStatusUnverified"),
                HealthSeverity.Warning,
                null,
                appsDirectory,
                GetLastChanged(bundlePath),
                L("HealthEvidenceSpotXUnverified"),
                "Reapply");
        }

        return Component(
            "spotx",
            L("HealthNameSpotXPatch"),
            L("HealthStatusBundleMissing"),
            HealthSeverity.Critical,
            null,
            appsDirectory,
            null,
            L("HealthEvidenceSpotXBundleMissing"),
            "Reapply",
            "FullReset");
    }

    private StackHealthComponent BuildSpicetifyCliComponent(bool installed)
    {
        if (!installed)
        {
            return Component(
                "spicetify-cli",
                L("HealthNameSpicetifyCli"),
                L("HealthStatusNotInstalled"),
                HealthSeverity.Info,
                null,
                _spicetifyPath,
                null,
                L("HealthEvidenceSpicetifyCliMissing"),
                "Install");
        }

        return Component(
            "spicetify-cli",
            L("HealthNameSpicetifyCli"),
            L("HealthStatusDetected"),
            HealthSeverity.Ready,
            _spicetifyVersionProbe(),
            _spicetifyPath,
            GetLastChanged(_spicetifyPath),
            L("HealthEvidenceSpicetifyCliDetected"));
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
                L("HealthNameSpicetifyConfig"),
                L("HealthStatusNotAvailable"),
                HealthSeverity.Info,
                null,
                configPath,
                null,
                L("HealthEvidenceSpicetifyConfigUnavailable"),
                "Install");
        }

        if (!File.Exists(configPath))
        {
            return Component(
                "spicetify-config",
                L("HealthNameSpicetifyConfig"),
                L("HealthStatusMissing"),
                HealthSeverity.Warning,
                null,
                configPath,
                null,
                L("HealthEvidenceSpicetifyConfigMissing"),
                "Reapply");
        }

        var currentTheme = config.TryGetValue("current_theme", out var theme) ? theme : string.Empty;
        var customApps = config.TryGetValue("custom_apps", out var apps) ? apps : string.Empty;
        var evidence = string.IsNullOrWhiteSpace(currentTheme)
            ? L("HealthEvidenceSpicetifyConfigNoTheme")
            : F("HealthEvidenceSpicetifyConfigThemeFormat", currentTheme);
        if (!string.IsNullOrWhiteSpace(customApps))
        {
            evidence += " " + F("HealthEvidenceSpicetifyConfigAppsFormat", customApps);
        }

        return Component(
            "spicetify-config",
            L("HealthNameSpicetifyConfig"),
            L("HealthStatusReadable"),
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
                L("HealthNameMarketplace"),
                L("HealthStatusNotAvailable"),
                HealthSeverity.Info,
                null,
                marketplaceDirectory,
                null,
                L("HealthEvidenceMarketplaceUnavailable"),
                "Install");
        }

        if (filesPresent && registered)
        {
            var evidence = BuildMarketplaceEvidenceText(visibilityEvidence, L("HealthEvidenceMarketplaceReady"));
            if (visibilityEvidence?.ApplySucceeded == false)
            {
                return Component(
                    "marketplace",
                    L("HealthNameMarketplace"),
                    L("HealthStatusApplyFailed"),
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
                    L("HealthNameMarketplace"),
                    L("HealthStatusOpenFailed"),
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
                    L("HealthNameMarketplace"),
                    L("HealthStatusLikelyVisible"),
                    HealthSeverity.Ready,
                    visibilityEvidence.ManifestVersion,
                    marketplaceDirectory,
                    GetNewestFileChange(marketplaceDirectory),
                    evidence,
                    "OpenMarketplace");
            }

            return Component(
                "marketplace",
                L("HealthNameMarketplace"),
                L("HealthStatusFilesInstalled"),
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
                L("HealthNameMarketplace"),
                L("HealthStatusHidden"),
                HealthSeverity.Warning,
                null,
                marketplaceDirectory,
                GetNewestFileChange(marketplaceDirectory),
                L("HealthEvidenceMarketplaceHidden"),
                "RepairMarketplace");
        }

        if (registered)
        {
            return Component(
                "marketplace",
                L("HealthNameMarketplace"),
                L("HealthStatusFilesMissing"),
                HealthSeverity.Warning,
                null,
                marketplaceDirectory,
                null,
                L("HealthEvidenceMarketplaceFilesMissing"),
                "RepairMarketplace");
        }

        return Component(
            "marketplace",
            L("HealthNameMarketplace"),
            L("HealthStatusNotEnabled"),
            HealthSeverity.Warning,
            null,
            marketplaceDirectory,
            null,
            L("HealthEvidenceMarketplaceNotEnabled"),
            "RepairMarketplace");
    }

    private static string BuildMarketplaceEvidenceText(MarketplaceVisibilityEvidence? visibilityEvidence, string fallback)
    {
        if (visibilityEvidence is null)
        {
            return fallback + " " + L("HealthEvidenceMarketplaceNoVisibility");
        }

        var manifest = string.IsNullOrWhiteSpace(visibilityEvidence.ManifestVersion)
            ? L("HealthEvidenceManifestUnknown")
            : F("HealthEvidenceManifestFormat", visibilityEvidence.ManifestVersion);
        var apply = visibilityEvidence.ApplySucceeded.HasValue
            ? F(
                "HealthEvidenceApplyResultFormat",
                visibilityEvidence.ApplyStage ?? L("HealthValueApply"),
                visibilityEvidence.ApplySucceeded.Value ? L("HealthValueSucceeded") : L("HealthValueFailed"))
            : L("HealthEvidenceApplyNotRecorded");
        var open = visibilityEvidence.OpenUriSucceeded.HasValue
            ? F("HealthEvidenceOpenResultFormat", visibilityEvidence.OpenUriSucceeded.Value ? L("HealthValueSucceeded") : L("HealthValueFailed"))
            : L("HealthEvidenceOpenNotRecorded");
        var spotify = visibilityEvidence.SpotifyRunningAfterOpen.HasValue
            ? visibilityEvidence.SpotifyRunningAfterOpen.Value ? L("HealthEvidenceSpotifyObserved") : L("HealthEvidenceSpotifyNotObserved")
            : visibilityEvidence.LastObservedSpotifySession ?? L("HealthValueUnknown");

        var text = F(
            "HealthEvidenceMarketplaceVisibilityFormat",
            fallback,
            visibilityEvidence.Source,
            manifest,
            apply,
            open,
            spotify,
            visibilityEvidence.LikelyVisible);

        if (!string.IsNullOrWhiteSpace(visibilityEvidence.ApplyMessage))
        {
            text += " " + F("HealthEvidenceApplyDetailFormat", visibilityEvidence.ApplyMessage);
        }

        if (!string.IsNullOrWhiteSpace(visibilityEvidence.OpenUriMessage))
        {
            text += " " + F("HealthEvidenceOpenDetailFormat", visibilityEvidence.OpenUriMessage);
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
                L("HealthNameActiveTheme"),
                L("HealthStatusNotAvailable"),
                HealthSeverity.Info,
                null,
                configPath,
                null,
                L("HealthEvidenceThemeUnavailable"),
                "Install");
        }

        var theme = config.TryGetValue("current_theme", out var currentTheme) ? currentTheme : string.Empty;
        var injectCss = config.TryGetValue("inject_css", out var injectCssValue) && IsEnabledValue(injectCssValue);
        var replaceColors = config.TryGetValue("replace_colors", out var replaceColorsValue) && IsEnabledValue(replaceColorsValue);

        if (string.IsNullOrWhiteSpace(theme) || string.Equals(theme, "SpicetifyDefault", StringComparison.OrdinalIgnoreCase))
        {
            return Component(
                "active-theme",
                L("HealthNameActiveTheme"),
                L("HealthStatusMarketplaceOrStock"),
                HealthSeverity.Ready,
                null,
                configPath,
                GetLastChanged(configPath),
                L("HealthEvidenceThemeStock"));
        }

        if (!injectCss || !replaceColors)
        {
            return Component(
                "active-theme",
                L("HealthNameActiveTheme"),
                L("HealthStatusInjectionDisabled"),
                HealthSeverity.Warning,
                null,
                configPath,
                GetLastChanged(configPath),
                F("HealthEvidenceThemeInjectionDisabledFormat", theme),
                "Reapply",
                "SafeMode");
        }

        return Component(
            "active-theme",
            L("HealthNameActiveTheme"),
            L("HealthStatusActive"),
            HealthSeverity.Ready,
            null,
            configPath,
            GetLastChanged(configPath),
            F("HealthEvidenceThemeActiveFormat", theme));
    }

    private StackHealthComponent BuildBackupComponent(bool spicetifyInstalled)
    {
        var backupCount = CountDirectories(_backupDirectory);
        if (backupCount > 0)
        {
            return Component(
                "backups",
                L("HealthNameBackups"),
                backupCount == 1 ? L("HealthStatusOneBackup") : F("HealthStatusBackupsFormat", backupCount),
                HealthSeverity.Ready,
                null,
                _backupDirectory,
                GetNewestDirectoryChange(_backupDirectory),
                L("HealthEvidenceBackupsAvailable"));
        }

        return Component(
            "backups",
            L("HealthNameBackups"),
            L("HealthStatusNoneYet"),
            spicetifyInstalled ? HealthSeverity.Warning : HealthSeverity.Info,
            null,
            _backupDirectory,
            null,
            spicetifyInstalled
                ? L("HealthEvidenceBackupsMissing")
                : L("HealthEvidenceBackupsPending"),
            spicetifyInstalled ? "CreateBackup" : "Install");
    }

    private static StackHealthComponent BuildWatcherComponent(string statePath, WatcherState state, bool taskRegistered)
    {
        if (!taskRegistered)
        {
            return Component(
                "auto-reapply-watcher",
                L("HealthNameWatcher"),
                L("HealthStatusDisabled"),
                HealthSeverity.Info,
                state.LastKnownVersion,
                statePath,
                state.LastRunAt,
                L("HealthEvidenceWatcherDisabled"),
                "EnableAutoReapply");
        }

        if (state.LastRunAt is null)
        {
            return Component(
                "auto-reapply-watcher",
                L("HealthNameWatcher"),
                L("HealthStatusNoRunRecorded"),
                HealthSeverity.Warning,
                state.LastKnownVersion,
                statePath,
                null,
                L("HealthEvidenceWatcherNoRun"),
                "WatchAutoReapply");
        }

        if (state.LastOutcome?.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Component(
                "auto-reapply-watcher",
                L("HealthNameWatcher"),
                L("HealthStatusLastTickFailed"),
                HealthSeverity.Warning,
                state.LastKnownVersion,
                statePath,
                state.LastRunAt,
                F("HealthEvidenceWatcherFailedFormat", state.LastOutcome),
                "WatchAutoReapply",
                "Reapply");
        }

        if (state.LastRunAt.Value < DateTime.Now.AddDays(-7))
        {
            return Component(
                "auto-reapply-watcher",
                L("HealthNameWatcher"),
                L("HealthStatusStale"),
                HealthSeverity.Warning,
                state.LastKnownVersion,
                statePath,
                state.LastRunAt,
                L("HealthEvidenceWatcherStale"),
                "WatchAutoReapply");
        }

        return Component(
            "auto-reapply-watcher",
            L("HealthNameWatcher"),
            LocalizeWatcherOutcome(state.LastOutcome),
            HealthSeverity.Ready,
            state.LastKnownVersion,
            statePath,
            state.LastRunAt,
            L("HealthEvidenceWatcherActive"));
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
                L("HealthNamePostUpdate"),
                L("HealthStatusNotApplicable"),
                HealthSeverity.Info,
                null,
                statePath,
                LatestStateChange(state),
                L("HealthEvidencePostUpdateNotApplicable"),
                "Install");
        }

        if (string.IsNullOrWhiteSpace(lastPatchedVersion))
        {
            return Component(
                "post-spotify-update",
                L("HealthNamePostUpdate"),
                autoReapplyTaskRegistered ? L("HealthStatusNoWatcherHistory") : L("HealthStatusWatcherNotEnabled"),
                HealthSeverity.Info,
                currentSpotifyVersion,
                statePath,
                LatestStateChange(state),
                autoReapplyTaskRegistered
                    ? L("HealthEvidencePostUpdateNoHistory")
                    : L("HealthEvidencePostUpdateWatcherDisabled"),
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
                    L("HealthNamePostUpdate"),
                    L("HealthStatusMarketplaceStillMissing"),
                    HealthSeverity.Warning,
                    currentSpotifyVersion,
                    statePath,
                    LatestStateChange(state),
                    F("HealthEvidencePostUpdateMarketplaceMissingFormat", currentSpotifyVersion, FormatMaybe(state.LastSuccessfulApplyAt), FormatMaybe(spicetifyVersion)),
                    "RepairMarketplace",
                    "OpenLogs");
            }

            return Component(
                "post-spotify-update",
                L("HealthNamePostUpdate"),
                L("HealthStatusReapplied"),
                HealthSeverity.Ready,
                currentSpotifyVersion,
                statePath,
                LatestStateChange(state),
                F("HealthEvidencePostUpdateReappliedFormat", FormatMaybe(state.LastSuccessfulApplyAt), FormatMaybe(spicetifyVersion)));
        }

        if (string.Equals(state.LastOutcome, "DeferredSpotifyRunning", StringComparison.OrdinalIgnoreCase) || (spotifyVersionChanged && _spotifyRunningProbe()))
        {
            return Component(
                "post-spotify-update",
                L("HealthNamePostUpdate"),
                L("HealthStatusCloseSpotifyFirst"),
                HealthSeverity.Warning,
                currentSpotifyVersion,
                statePath,
                LatestStateChange(state),
                F("HealthEvidencePostUpdateDeferredFormat", FormatMaybe(lastPatchedVersion), FormatMaybe(currentSpotifyVersion)),
                "Reapply",
                "OpenLogs");
        }

        if (state.LastOutcome?.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) == true)
        {
            var failedDuringSpotX = state.LastOutcome.Contains("spotx", StringComparison.OrdinalIgnoreCase);
            return Component(
                "post-spotify-update",
                L("HealthNamePostUpdate"),
                failedDuringSpotX ? L("HealthStatusSpotXReapplyFailed") : L("HealthStatusWatcherFailed"),
                failedDuringSpotX ? HealthSeverity.Critical : HealthSeverity.Warning,
                currentSpotifyVersion,
                statePath,
                LatestStateChange(state),
                F("HealthEvidencePostUpdateWatcherFailedFormat", FormatMaybe(lastPatchedVersion), FormatMaybe(currentSpotifyVersion), state.LastOutcome),
                "Reapply",
                "OpenLogs");
        }

        if (string.Equals(state.LastApplyOutcome, "SpicetifyApplyRolledBack", StringComparison.OrdinalIgnoreCase) ||
            state.LastApplyError?.Contains("restored Spotify to a usable state", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Component(
                "post-spotify-update",
                L("HealthNamePostUpdate"),
                L("HealthStatusSpicetifyRolledBack"),
                HealthSeverity.Warning,
                currentSpotifyVersion,
                statePath,
                LatestStateChange(state),
                F("HealthEvidencePostUpdateRolledBackFormat", FormatMaybe(state.LastApplyError)),
                "Reapply",
                "RestoreVanilla",
                "OpenLogs");
        }

        if (spotifyVersionChanged)
        {
            return Component(
                "post-spotify-update",
                L("HealthNamePostUpdate"),
                L("HealthStatusReapplyNeeded"),
                HealthSeverity.Warning,
                currentSpotifyVersion,
                statePath,
                LatestStateChange(state),
                F("HealthEvidencePostUpdateReapplyNeededFormat", FormatMaybe(lastPatchedVersion), FormatMaybe(currentSpotifyVersion)),
                "Reapply",
                "OpenLogs");
        }

        return Component(
            "post-spotify-update",
            L("HealthNamePostUpdate"),
            L("HealthStatusNoDrift"),
            HealthSeverity.Ready,
            currentSpotifyVersion,
            statePath,
            LatestStateChange(state),
            F("HealthEvidencePostUpdateNoDriftFormat", FormatMaybe(spicetifyVersion)));
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
                L("HealthNameLogs"),
                L("HealthStatusNoLogsYet"),
                HealthSeverity.Info,
                null,
                installLogPath,
                null,
                L("HealthEvidenceLogsMissing"));
        }

        return Component(
            "logs",
            L("HealthNameLogs"),
            L("HealthStatusAvailable"),
            HealthSeverity.Ready,
            null,
            installLogChanged is null ? _rollingLogDirectory : installLogPath,
            latest,
            L("HealthEvidenceLogsAvailable"));
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
                L("HealthNameCrashReports"),
                recentCrashCount == 1 ? L("HealthStatusOneRecentCrash") : F("HealthStatusRecentCrashesFormat", recentCrashCount),
                HealthSeverity.Warning,
                null,
                _crashDirectory,
                newest,
                L("HealthEvidenceRecentCrashes"),
                "OpenLogs");
        }

        return Component(
            "crash-reports",
            L("HealthNameCrashReports"),
            L("HealthStatusNoRecentCrashes"),
            HealthSeverity.Ready,
            null,
            _crashDirectory,
            newest,
            crashFiles.Length == 0
                ? L("HealthEvidenceNoCrashReports")
                : L("HealthEvidenceNoRecentCrashes"));
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
                L("HealthNameSavedProfile"),
                L("HealthStatusAvailable"),
                HealthSeverity.Ready,
                null,
                configPath,
                GetLastChanged(configPath),
                L("HealthEvidenceSavedProfileAvailable"));
        }

        return Component(
            "saved-profile",
            L("HealthNameSavedProfile"),
            configFolderExists ? L("HealthStatusNotSaved") : L("HealthStatusProfileFolderMissing"),
            HealthSeverity.Info,
            null,
            configPath,
            null,
            configFolderExists
                ? L("HealthEvidenceSavedProfileNotSaved")
                : L("HealthEvidenceSavedProfileFolderMissing"),
            "Install");
    }

    private static StackHealthComponent BuildAssetCacheComponent(AssetCacheInventoryReport report)
    {
        var newest = report.Entries
            .Select(entry => entry.LastVerifiedAtUtc?.LocalDateTime ?? entry.LastUsedAtUtc?.LocalDateTime ?? entry.FirstSeenAtUtc?.LocalDateTime)
            .Where(changed => changed.HasValue)
            .OrderByDescending(changed => changed)
            .FirstOrDefault();
        var summary = F("HealthEvidenceCacheSummaryFormat", report.EntryCount, report.PresentCount, FormatBytes(report.TotalBytes));

        if (report.CorruptCount > 0)
        {
            return Component(
                "asset-cache",
                L("HealthNameAssetCache"),
                report.CorruptCount == 1 ? L("HealthStatusOneCorruptEntry") : F("HealthStatusCorruptEntriesFormat", report.CorruptCount),
                HealthSeverity.Warning,
                null,
                report.CacheDirectory,
                newest,
                F("HealthEvidenceCacheCorruptFormat", summary, report.CorruptCount),
                "ClearCache");
        }

        if (report.StaleCount > 0)
        {
            return Component(
                "asset-cache",
                L("HealthNameAssetCache"),
                report.StaleCount == 1 ? L("HealthStatusOneStaleEntry") : F("HealthStatusStaleEntriesFormat", report.StaleCount),
                HealthSeverity.Info,
                null,
                report.CacheDirectory,
                newest,
                F("HealthEvidenceCacheStaleFormat", summary),
                "ClearCache");
        }

        if (report.EntryCount == 0)
        {
            return Component(
                "asset-cache",
                L("HealthNameAssetCache"),
                L("HealthStatusEmpty"),
                HealthSeverity.Info,
                null,
                report.CacheDirectory,
                null,
                L("HealthEvidenceCacheEmpty"));
        }

        return Component(
            "asset-cache",
            L("HealthNameAssetCache"),
            report.EntryCount == 1 ? L("HealthStatusOneEntry") : F("HealthStatusEntriesFormat", report.EntryCount),
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
                L("HealthNameExtensionFiles"),
                L("HealthStatusNotApplicable"),
                HealthSeverity.Info,
                null,
                extensionsDir,
                null,
                L("HealthEvidenceExtensionsNotApplicable"));
        }

        if (!spicetifyConfig.TryGetValue("extensions", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return Component(
                "extension-integrity",
                L("HealthNameExtensionFiles"),
                L("HealthStatusNoneRegistered"),
                HealthSeverity.Ready,
                null,
                extensionsDir,
                null,
                L("HealthEvidenceExtensionsNoneRegistered"));
        }

        var registered = raw
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
        if (registered.Length == 0)
        {
            return Component(
                "extension-integrity",
                L("HealthNameExtensionFiles"),
                L("HealthStatusNoneRegistered"),
                HealthSeverity.Ready,
                null,
                extensionsDir,
                null,
                L("HealthEvidenceExtensionsNoneRegistered"));
        }

        var invalid = registered
            .Where(ext => !IsSafeExtensionFileName(ext))
            .ToArray();
        if (invalid.Length > 0)
        {
            var label = invalid.Length == 1 ? L("HealthStatusInvalidEntry") : F("HealthStatusInvalidEntriesFormat", invalid.Length);
            return Component(
                "extension-integrity",
                L("HealthNameExtensionFiles"),
                label,
                HealthSeverity.Warning,
                null,
                extensionsDir,
                null,
                L("HealthEvidenceExtensionsInvalid"),
                "Reapply");
        }

        var missing = registered
            .Where(ext => !File.Exists(Path.Combine(extensionsDir, ext)))
            .ToArray();

        if (missing.Length > 0)
        {
            var label = missing.Length == 1 ? F("HealthStatusExtensionMissingFormat", missing[0]) : F("HealthStatusExtensionFilesMissingFormat", missing.Length);
            return Component(
                "extension-integrity",
                L("HealthNameExtensionFiles"),
                label,
                HealthSeverity.Warning,
                null,
                extensionsDir,
                null,
                F("HealthEvidenceExtensionsMissingFormat", string.Join(", ", missing)),
                "Reapply");
        }

        return Component(
            "extension-integrity",
            L("HealthNameExtensionFiles"),
            L("HealthStatusAllPresent"),
            HealthSeverity.Ready,
            null,
            extensionsDir,
            GetNewestFileChange(extensionsDir),
            F("HealthEvidenceExtensionsAllPresentFormat", registered.Length));
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

    private static string L(string key) =>
        Strings.ResourceManager.GetString(key, Strings.Culture ?? CultureInfo.CurrentUICulture) ?? key;

    private static string F(string key, params object?[] args) =>
        string.Format(Strings.Culture ?? CultureInfo.CurrentCulture, L(key), args);

    private static string LocalizeWatcherOutcome(string? outcome) => outcome switch
    {
        null or "" => L("HealthStatusActive"),
        "Initialized" => L("HealthStatusWatcherInitialized"),
        "UpToDate" => L("HealthStatusWatcherUpToDate"),
        "DeferredSpotifyRunning" => L("HealthStatusWatcherDeferred"),
        "NoConfig" => L("HealthStatusWatcherNoConfig"),
        "PreferenceOff" => L("HealthStatusWatcherPreferenceOff"),
        "Reapplied" => L("HealthStatusReapplied"),
        _ => F("HealthStatusWatcherOutcomeFormat", outcome)
    };

    private static string FormatCacheAge(TimeSpan? age)
    {
        if (!age.HasValue)
        {
            return L("HealthValueNone");
        }

        if (age.Value.TotalMinutes < 1)
        {
            return F("HealthCacheAgeSecondsFormat", Math.Max(0, Math.Round(age.Value.TotalSeconds)));
        }

        if (age.Value.TotalHours < 1)
        {
            return F("HealthCacheAgeMinutesFormat", Math.Round(age.Value.TotalMinutes));
        }

        if (age.Value.TotalDays < 1)
        {
            return F("HealthCacheAgeHoursFormat", Math.Round(age.Value.TotalHours));
        }

        return F("HealthCacheAgeDaysFormat", Math.Round(age.Value.TotalDays));
    }

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
        value?.ToString("yyyy-MM-dd HH:mm") ?? L("HealthValueUnknown");

    private static string FormatMaybe(string? value) =>
        string.IsNullOrWhiteSpace(value) ? L("HealthValueUnknown") : value;

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
