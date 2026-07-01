using System.Collections.ObjectModel;
using LibreSpot.Desktop.Properties;

namespace LibreSpot.Desktop.Models;

public sealed class InstallConfiguration
{
    public int ConfigSchemaVersion { get; set; } = AppCatalog.CurrentConfigSchemaVersion;
    public string Mode { get; set; } = "Easy";
    public string UiCulture { get; set; } = AppCatalog.DefaultUiCulture;
    public bool CleanInstall { get; set; } = true;
    public bool LaunchAfter { get; set; } = true;

    public bool SpotX_NewTheme { get; set; } = true;
    public bool SpotX_PodcastsOff { get; set; } = true;
    public bool SpotX_BlockUpdate { get; set; } = true;
    public bool SpotX_AdSectionsOff { get; set; } = true;
    public bool SpotX_Premium { get; set; }
    public bool SpotX_LyricsEnabled { get; set; } = true;
    public string SpotX_LyricsTheme { get; set; } = "spotify";
    public bool SpotX_TopSearch { get; set; }
    public bool SpotX_RightSidebarOff { get; set; }
    public bool SpotX_RightSidebarClr { get; set; }
    public bool SpotX_CanvasHomeOff { get; set; }
    public bool SpotX_HomeSubOff { get; set; }
    public bool SpotX_DisableStartup { get; set; } = true;
    public bool SpotX_NoShortcut { get; set; }
    public int SpotX_CacheLimit { get; set; }
    public bool SpotX_Plus { get; set; }
    public bool SpotX_NewFullscreen { get; set; }
    public bool SpotX_FunnyProgress { get; set; }
    public bool SpotX_ExpSpotify { get; set; }
    public bool SpotX_LyricsBlock { get; set; }
    public bool SpotX_OldLyrics { get; set; }
    public bool SpotX_HideColIconOff { get; set; }
    public bool SpotX_SendVersionOff { get; set; } = true;
    public bool SpotX_StartSpoti { get; set; }
    public bool SpotX_DevTools { get; set; }
    public bool SpotX_Mirror { get; set; }
    public string SpotX_DownloadMethod { get; set; } = "";
    public bool SpotX_ConfirmUninstall { get; set; }
    public string SpotX_SpotifyVersionId { get; set; } = "auto";
    public string SpotX_Language { get; set; } = string.Empty;
    public bool SpotX_CustomPatchesEnabled { get; set; }
    public string SpotX_CustomPatchesJson { get; set; } = string.Empty;
    public string SpotX_CustomPatchesSourceUrl { get; set; } = string.Empty;
    public DateTimeOffset? SpotX_CustomPatchesFetchedAtUtc { get; set; }
    public int SpotX_CustomPatchesSourceByteCount { get; set; }
    public string SpotX_CustomPatchesSourceSha256 { get; set; } = string.Empty;

    public string Spicetify_Theme { get; set; } = "(None - Marketplace Only)";
    public string Spicetify_Scheme { get; set; } = "Default";
    public bool Spicetify_Marketplace { get; set; } = true;
    public List<string> Spicetify_Extensions { get; set; } = new() { "fullAppDisplay.js", "shuffle+.js", "trashbin.js" };
    public List<string> Spicetify_CustomApps { get; set; } = new();

    // Track 4.2 auto-reapply watcher. The PowerShell side owns the scheduled
    // task; the WPF shell round-trips the preference so toggling from either
    // UI stays consistent after a save/reload.
    public bool AutoReapply_Enabled { get; set; }

    // First-run ToS risk acknowledgment. Once the user accepts, this is
    // persisted as true and the dialog never shows again.
    public bool RiskAcknowledged { get; set; }

    public InstallConfiguration Clone() =>
        new()
        {
            ConfigSchemaVersion = ConfigSchemaVersion,
            Mode = Mode,
            UiCulture = UiCulture,
            CleanInstall = CleanInstall,
            LaunchAfter = LaunchAfter,
            SpotX_NewTheme = SpotX_NewTheme,
            SpotX_PodcastsOff = SpotX_PodcastsOff,
            SpotX_BlockUpdate = SpotX_BlockUpdate,
            SpotX_AdSectionsOff = SpotX_AdSectionsOff,
            SpotX_Premium = SpotX_Premium,
            SpotX_LyricsEnabled = SpotX_LyricsEnabled,
            SpotX_LyricsTheme = SpotX_LyricsTheme,
            SpotX_TopSearch = SpotX_TopSearch,
            SpotX_RightSidebarOff = SpotX_RightSidebarOff,
            SpotX_RightSidebarClr = SpotX_RightSidebarClr,
            SpotX_CanvasHomeOff = SpotX_CanvasHomeOff,
            SpotX_HomeSubOff = SpotX_HomeSubOff,
            SpotX_DisableStartup = SpotX_DisableStartup,
            SpotX_NoShortcut = SpotX_NoShortcut,
            SpotX_CacheLimit = SpotX_CacheLimit,
            SpotX_Plus = SpotX_Plus,
            SpotX_NewFullscreen = SpotX_NewFullscreen,
            SpotX_FunnyProgress = SpotX_FunnyProgress,
            SpotX_ExpSpotify = SpotX_ExpSpotify,
            SpotX_LyricsBlock = SpotX_LyricsBlock,
            SpotX_OldLyrics = SpotX_OldLyrics,
            SpotX_HideColIconOff = SpotX_HideColIconOff,
            SpotX_SendVersionOff = SpotX_SendVersionOff,
            SpotX_StartSpoti = SpotX_StartSpoti,
            SpotX_DevTools = SpotX_DevTools,
            SpotX_Mirror = SpotX_Mirror,
            SpotX_DownloadMethod = SpotX_DownloadMethod,
            SpotX_ConfirmUninstall = SpotX_ConfirmUninstall,
            SpotX_SpotifyVersionId = SpotX_SpotifyVersionId,
            SpotX_Language = SpotX_Language,
            SpotX_CustomPatchesEnabled = SpotX_CustomPatchesEnabled,
            SpotX_CustomPatchesJson = SpotX_CustomPatchesJson,
            SpotX_CustomPatchesSourceUrl = SpotX_CustomPatchesSourceUrl,
            SpotX_CustomPatchesFetchedAtUtc = SpotX_CustomPatchesFetchedAtUtc,
            SpotX_CustomPatchesSourceByteCount = SpotX_CustomPatchesSourceByteCount,
            SpotX_CustomPatchesSourceSha256 = SpotX_CustomPatchesSourceSha256,
            Spicetify_Theme = Spicetify_Theme,
            Spicetify_Scheme = Spicetify_Scheme,
            Spicetify_Marketplace = Spicetify_Marketplace,
            Spicetify_Extensions = new List<string>(Spicetify_Extensions ?? []),
            Spicetify_CustomApps = new List<string>(Spicetify_CustomApps ?? []),
            AutoReapply_Enabled = AutoReapply_Enabled,
            RiskAcknowledged = RiskAcknowledged
        };
}

public sealed record OptionDefinition(string Key, string Title, string Description, string Section);
public sealed record ExtensionDefinition(string Key, string Title, string Description);
public sealed record CustomAppDefinition(string Key, string Title, string Description);
public sealed record MaintenanceActionDefinition(string Action, string Title, string Description, string ButtonText, bool IsDestructive = false);
public sealed record UpstreamDependencyPin(
    string Id,
    string Name,
    string PinnedValue,
    string ValueKind,
    string GitRepository,
    string GitReferencePattern,
    string? RestLatestReleaseApi,
    string? ValuePrefixToStrip);

public sealed class UpstreamDriftReport
{
    public static UpstreamDriftReport Empty { get; } = new(Array.Empty<UpstreamDependencyState>(), DateTimeOffset.UtcNow);

    public UpstreamDriftReport(IEnumerable<UpstreamDependencyState> dependencies, DateTimeOffset generatedAtUtc)
    {
        Dependencies = new ReadOnlyCollection<UpstreamDependencyState>(dependencies.ToArray());
        GeneratedAtUtc = generatedAtUtc;
    }

    public IReadOnlyList<UpstreamDependencyState> Dependencies { get; }
    public DateTimeOffset GeneratedAtUtc { get; }
    public bool IsDegraded => Dependencies.Any(dependency => dependency.IsDegraded);
}

public sealed record UpstreamDependencyState(
    string Id,
    string Name,
    string PinnedValue,
    string CurrentValue,
    string? LatestValue,
    string DriftState,
    string MetadataSource,
    DateTimeOffset CheckedAtUtc,
    TimeSpan? CacheAge,
    bool IsDegraded,
    string Evidence);

public sealed record CommunityAssetPin(
    string Id,
    string Kind,
    string Name,
    string Owner,
    string Repository,
    string Branch,
    string PinnedCommit,
    string? PinnedHash,
    string? SourceUrl,
    string License,
    string SupportState,
    string FallbackBehavior,
    string NetworkBehavior,
    string? NetworkDetail,
    bool RequiresTrustReview);

public sealed class CommunityAssetDriftReport
{
    public static CommunityAssetDriftReport Empty { get; } = new(Array.Empty<CommunityAssetState>(), DateTimeOffset.UtcNow);

    public CommunityAssetDriftReport(IEnumerable<CommunityAssetState> assets, DateTimeOffset generatedAtUtc)
    {
        Assets = new ReadOnlyCollection<CommunityAssetState>(assets.ToArray());
        GeneratedAtUtc = generatedAtUtc;
    }

    public IReadOnlyList<CommunityAssetState> Assets { get; }
    public DateTimeOffset GeneratedAtUtc { get; }
    public bool IsDegraded => Assets.Any(asset => asset.IsDegraded);
    public bool HasMissingAssets => Assets.Any(asset => string.Equals(asset.DriftState, "missing", StringComparison.OrdinalIgnoreCase));
    public bool HasReviewRequiredAssets => Assets.Any(asset => asset.RequiresTrustReview);
}

public sealed record CommunityAssetState(
    string Id,
    string Kind,
    string Name,
    string SourceUrl,
    string GitRepository,
    string GitReference,
    string PinnedCommit,
    string? PinnedHash,
    string? LatestCommit,
    string DriftState,
    string MetadataSource,
    DateTimeOffset CheckedAtUtc,
    TimeSpan? CacheAge,
    bool IsDegraded,
    string License,
    string SupportState,
    string FallbackBehavior,
    string NetworkBehavior,
    string? NetworkDetail,
    bool RequiresTrustReview,
    string Evidence);

public sealed class AssetCacheInventoryReport
{
    public static AssetCacheInventoryReport Empty { get; } = new(
        Array.Empty<AssetCacheEntryState>(),
        string.Empty,
        string.Empty,
        DateTimeOffset.UtcNow);

    public AssetCacheInventoryReport(
        IEnumerable<AssetCacheEntryState> entries,
        string cacheDirectory,
        string indexPath,
        DateTimeOffset generatedAtUtc)
    {
        Entries = new ReadOnlyCollection<AssetCacheEntryState>(entries.ToArray());
        CacheDirectory = cacheDirectory;
        IndexPath = indexPath;
        GeneratedAtUtc = generatedAtUtc;

        EntryCount = Entries.Count;
        PresentCount = Entries.Count(entry => string.Equals(entry.Status, "present", StringComparison.OrdinalIgnoreCase));
        MissingCount = Entries.Count(entry => string.Equals(entry.Status, "missing", StringComparison.OrdinalIgnoreCase));
        CorruptCount = Entries.Count(entry => string.Equals(entry.Status, "corrupt", StringComparison.OrdinalIgnoreCase));
        UnindexedCount = Entries.Count(entry => string.Equals(entry.Status, "unindexed", StringComparison.OrdinalIgnoreCase));
        StaleCount = MissingCount + UnindexedCount;
        TotalBytes = Entries.Where(entry => entry.FilePresent).Sum(entry => entry.ByteSize);
        HasIssues = CorruptCount > 0 || StaleCount > 0;
    }

    public IReadOnlyList<AssetCacheEntryState> Entries { get; }
    public string CacheDirectory { get; }
    public string IndexPath { get; }
    public DateTimeOffset GeneratedAtUtc { get; }
    public int EntryCount { get; }
    public int PresentCount { get; }
    public int MissingCount { get; }
    public int CorruptCount { get; }
    public int UnindexedCount { get; }
    public int StaleCount { get; }
    public long TotalBytes { get; }
    public bool HasIssues { get; }
}

public sealed record AssetCacheEntryState(
    string Sha256,
    string Label,
    string? SourceUrl,
    long ByteSize,
    DateTimeOffset? FirstSeenAtUtc,
    DateTimeOffset? LastUsedAtUtc,
    DateTimeOffset? LastVerifiedAtUtc,
    string Status,
    string Path,
    bool FilePresent,
    string Evidence);

public sealed record MarketplaceVisibilityEvidence(
    int SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string Source,
    bool FilesPresent,
    bool Registered,
    bool LikelyVisible,
    string MarketplaceStatus,
    string MarketplacePath,
    string? ManifestVersion,
    string? ApplyStage,
    bool? ApplySucceeded,
    string? ApplyMessage,
    DateTimeOffset? ApplyCompletedAtUtc,
    bool? OpenUriSucceeded,
    string? OpenUriMessage,
    DateTimeOffset? OpenUriRequestedAtUtc,
    bool? SpotifyRunningAfterOpen,
    string LastObservedSpotifySession,
    DateTimeOffset? LastObservedAtUtc);

public static class HealthSeverity
{
    public const string Ready = "ready";
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Critical = "critical";
}

public sealed class StackHealthReport
{
    public static StackHealthReport Empty { get; } = new(Array.Empty<StackHealthComponent>());

    public StackHealthReport(IEnumerable<StackHealthComponent> components)
    {
        Components = new ReadOnlyCollection<StackHealthComponent>(components.ToArray());
        CriticalIssues = new ReadOnlyCollection<StackHealthComponent>(
            Components.Where(component => component.Severity == HealthSeverity.Critical).ToArray());
        WarningIssues = new ReadOnlyCollection<StackHealthComponent>(
            Components.Where(component => component.Severity == HealthSeverity.Warning).ToArray());
        InfoIssues = new ReadOnlyCollection<StackHealthComponent>(
            Components.Where(component => component.Severity == HealthSeverity.Info).ToArray());
    }

    public IReadOnlyList<StackHealthComponent> Components { get; }
    public IReadOnlyList<StackHealthComponent> CriticalIssues { get; }
    public IReadOnlyList<StackHealthComponent> WarningIssues { get; }
    public IReadOnlyList<StackHealthComponent> InfoIssues { get; }

    public bool HasIssues => CriticalIssues.Count + WarningIssues.Count + InfoIssues.Count > 0;
    public bool HasCriticalIssues => CriticalIssues.Count > 0;
    public bool HasWarningIssues => WarningIssues.Count > 0;
    public bool HasInfoIssues => InfoIssues.Count > 0;

    public string StatusTitle
    {
        get
        {
            var spotify = Find("spotify");
            var spicetify = Find("spicetify-cli");

            if (spotify?.Severity == HealthSeverity.Info && spicetify?.Severity == HealthSeverity.Info)
            {
                return "Clean slate";
            }

            if (HasCriticalIssues)
            {
                return "Needs repair";
            }

            if (HasWarningIssues)
            {
                return "Review recommended";
            }

            return "Stack ready";
        }
    }

    public string StatusDetail
    {
        get
        {
            var spotify = Find("spotify");
            var spicetify = Find("spicetify-cli");

            if (spotify?.Severity == HealthSeverity.Info && spicetify?.Severity == HealthSeverity.Info)
            {
                return "LibreSpot will build a fresh SpotX + Spicetify stack.";
            }

            if (HasCriticalIssues)
            {
                return "A required file or state check needs repair before the stack is reliable.";
            }

            if (HasWarningIssues)
            {
                return "The stack is present, with targeted repairs or backups recommended.";
            }

            return "Install, refresh, or roll back from here.";
        }
    }

    public string IssueSummary
    {
        get
        {
            var count = CriticalIssues.Count + WarningIssues.Count;
            return count switch
            {
                0 when InfoIssues.Count == 0 => "No issues detected",
                0 => $"{InfoIssues.Count} informational note{(InfoIssues.Count == 1 ? string.Empty : "s")}",
                1 => "1 repair note",
                _ => $"{count} repair notes"
            };
        }
    }

    private StackHealthComponent? Find(string id) =>
        Components.FirstOrDefault(component => string.Equals(component.Id, id, StringComparison.OrdinalIgnoreCase));
}

public sealed record StackHealthComponent(
    string Id,
    string Name,
    string Status,
    string Severity,
    string? DetectedVersion,
    string? Path,
    DateTime? LastChanged,
    string Evidence,
    IReadOnlyList<string> RecommendedActionIds)
{
    public bool HasDetectedVersion => !string.IsNullOrWhiteSpace(DetectedVersion);
    public bool HasPath => !string.IsNullOrWhiteSpace(Path);
    public bool HasLastChanged => LastChanged.HasValue;
    public bool HasRecommendedActions => RecommendedActionIds.Count > 0;
    public string LastChangedDisplay => LastChanged?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
    public string RecommendedActionText => HasRecommendedActions
        ? string.Join(", ", RecommendedActionIds)
        : "No action";
}

public static class Prettify
{
    /// <summary>
    /// Converts a kebab/snake slug into a human label for display in dropdowns.
    /// Preserves parenthesized sentinels like "(None - Marketplace Only)" verbatim.
    /// </summary>
    public static string Label(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return slug;
        }

        if (slug.StartsWith("(", StringComparison.Ordinal))
        {
            return slug;
        }

        var replaced = slug
            .Replace('-', ' ')
            .Replace('_', ' ')
            .Replace('+', ' ')
            .Replace(".js", string.Empty, StringComparison.OrdinalIgnoreCase);

        var parts = replaced.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.Length == 0)
            {
                continue;
            }

            if (part.Length <= 3 && part.All(char.IsUpper))
            {
                continue;
            }

            parts[i] = char.ToUpperInvariant(part[0]) + part[1..];
        }

        return string.Join(' ', parts);
    }
}

public sealed class EnvironmentSnapshot
{
    public bool SpotifyInstalled { get; init; }
    public bool SpicetifyInstalled { get; init; }
    public bool MarketplaceFilesPresent { get; init; }
    public bool MarketplaceRegistered { get; init; }
    public bool SavedConfigExists { get; init; }
    public bool ConfigFolderExists { get; init; }
    public bool AutoReapplyTaskRegistered { get; init; }
    public StackHealthReport HealthReport { get; init; } = StackHealthReport.Empty;
    public UpstreamDriftReport UpstreamDriftReport { get; init; } = UpstreamDriftReport.Empty;
    public CommunityAssetDriftReport CommunityAssetDriftReport { get; init; } = CommunityAssetDriftReport.Empty;
    public AssetCacheInventoryReport AssetCacheInventory { get; init; } = AssetCacheInventoryReport.Empty;
    public MarketplaceVisibilityEvidence? MarketplaceVisibilityEvidence { get; init; }
    public string HostArchitecture { get; init; } = "Unknown";
    public string ProcessArchitecture { get; init; } = "Unknown";
    public bool MarketplaceReady => MarketplaceFilesPresent && MarketplaceRegistered;
    public bool MarketplaceLikelyVisible => MarketplaceVisibilityEvidence?.LikelyVisible == true;

    public string StatusTitle => HealthReport.StatusTitle;

    public string StatusDetail => HealthReport.StatusDetail;
}

public static class AppCatalog
{
    public const int CurrentConfigSchemaVersion = 1;
    public const string DefaultUiCulture = "en";
    public const string PinnedSpotXVersion = "2.0";
    public const string PinnedSpotXCommit = "3284673df69e276c5c0ee90bb1cc9185cecb9ad4";
    public const string PinnedSpotXSpotifyVersionId = "1.2.92";
    public const string PinnedSpotXSpotifyVersion = "1.2.92";
    public const string PinnedSpicetifyCliVersion = "2.43.2";
    public const string SpicetifyWindowsMinTestedSpotify = "1.2.14";
    public const string SpicetifyWindowsMaxTestedSpotify = "1.2.88";
    public const string PinnedMarketplaceVersion = "1.0.8";
    public const string PinnedThemesCommit = "df033493a7dae30ca6e371de9cec1897871dbb0c";
    public const string PinnedStatsCustomAppVersion = "1.1.3";
    public const string PinnedStatsCustomAppReleaseTag = "stats-v1.1.3";

    public static IReadOnlyList<UpstreamDependencyPin> UpstreamDependencyPins { get; } =
        new ReadOnlyCollection<UpstreamDependencyPin>(new[]
        {
            new UpstreamDependencyPin(
                "spotx",
                "SpotX",
                PinnedSpotXCommit,
                "commit",
                "https://github.com/SpotX-Official/SpotX.git",
                "refs/heads/main",
                null,
                null),
            new UpstreamDependencyPin(
                "spicetify-cli",
                "Spicetify CLI",
                PinnedSpicetifyCliVersion,
                "version",
                "https://github.com/spicetify/cli.git",
                "refs/tags/v*",
                "https://api.github.com/repos/spicetify/cli/releases/latest",
                "v"),
            new UpstreamDependencyPin(
                "marketplace",
                "Marketplace",
                PinnedMarketplaceVersion,
                "version",
                "https://github.com/spicetify/marketplace.git",
                "refs/tags/v*",
                "https://api.github.com/repos/spicetify/marketplace/releases/latest",
                "v"),
            new UpstreamDependencyPin(
                "themes",
                "Spicetify themes",
                PinnedThemesCommit,
                "commit",
                "https://github.com/spicetify/spicetify-themes.git",
                "refs/heads/master",
                null,
                null),
            new UpstreamDependencyPin(
                "stats",
                "Stats custom app",
                PinnedStatsCustomAppReleaseTag,
                "version",
                "https://github.com/harbassan/spicetify-apps.git",
                "refs/tags/stats-v*",
                null,
                "stats-v")
        });

    public static IReadOnlyList<string> SupportedUiCultures { get; } =
        new ReadOnlyCollection<string>(new[] { DefaultUiCulture, "ru", "zh-Hans", "pt-BR", "es" });

    public static string NormalizeUiCulture(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return DefaultUiCulture;
        }

        var normalized = SupportedUiCultures.FirstOrDefault(culture =>
            string.Equals(culture, cultureName.Trim(), StringComparison.OrdinalIgnoreCase));
        return normalized ?? DefaultUiCulture;
    }

    public static IReadOnlyList<string> LyricsThemes { get; } = new ReadOnlyCollection<string>(new[]
    {
        "spotify", "blueberry", "blue", "discord", "forest", "fresh", "github", "lavender",
        "orange", "pumpkin", "purple", "red", "strawberry", "turquoise", "yellow", "oceano",
        "royal", "krux", "pinkle", "zing", "radium", "sandbar", "postlight", "relish",
        "drot", "default", "spotify#2"
    });

    public sealed record SpotifyVersionEntry(string Id, string Label, string Version, string Notes, string Architecture = "any");
    public sealed record DownloadMethodEntry(string Id, string Label, string Detail);

    public static IReadOnlyList<SpotifyVersionEntry> SpotifyVersionManifest { get; } = new ReadOnlyCollection<SpotifyVersionEntry>(new[]
    {
        new SpotifyVersionEntry("auto",            "Auto (use SpotX default)",          "",                          "Recommended. Lets SpotX pick the most compatible build.", "any"),
        new SpotifyVersionEntry(PinnedSpotXSpotifyVersionId, "1.2.92 (current pinned)", PinnedSpotXSpotifyVersion, "Best match for our pinned SpotX commit; newer than Spicetify CLI's max-tested Windows CSS-map baseline.", "x64"),
        new SpotifyVersionEntry("1.2.90.451",      "1.2.90.451 (previous fallback)",    "1.2.90.451.gb094aab0",      "Prior pinned build kept for rollback and comparison.", "x64"),
        new SpotifyVersionEntry("1.2.86.502",      "1.2.86.502 (older stable)",         "1.2.86.502.g8cd7fb22",      "Earlier pinned build kept for rollback and comparison.", "x64"),
        new SpotifyVersionEntry("1.2.85.519",      "1.2.85.519 (older stable)",         "1.2.85.519.g7c42e2e8",      "Last Windows release before Canvas-home changes.", "x64"),
        new SpotifyVersionEntry("1.2.53.440.x86",  "1.2.53.440 (x86 / 32-bit only)",    "1.2.53.440.g7b2f582a",      "For 32-bit Windows. Do not pick on x64.", "x86"),
        new SpotifyVersionEntry("1.2.5.1006.win7", "1.2.5.1006 (Windows 7 / 8.1)",      "1.2.5.1006.g22820f93",      "Last build supported on legacy Windows.", "legacy-os"),
    });

    public static IReadOnlyList<DownloadMethodEntry> DownloadMethods { get; } = new ReadOnlyCollection<DownloadMethodEntry>(new[]
    {
        new DownloadMethodEntry("", "Automatic (recommended)", "LibreSpot uses the backend's default download flow and falls back only when it needs to."),
        new DownloadMethodEntry("curl", "Force cURL", "Useful when the default web stack is flaky, filtered, or noticeably slower on your network."),
        new DownloadMethodEntry("webclient", "Force WebClient", "Legacy .NET transfer path for older or tightly managed Windows environments."),
    });

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ThemeSchemes { get; } =
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["(None - Marketplace Only)"] = new[] { "Default" },
            ["Sleek"] = new[] { "Wealthy", "Cherry", "Coral", "Deep", "Greener", "Deeper", "Psycho", "UltraBlack", "Nord", "Futura", "Elementary", "BladeRunner", "Dracula", "VantaBlack", "RosePine", "Eldritch", "Catppuccin", "AyuDark", "TokyoNight" },
            ["Dribbblish"] = new[] { "base", "white", "dark", "dracula", "nord-light", "nord-dark", "purple", "samurai", "beach-sunset", "gruvbox", "gruvbox-material-dark", "rosepine", "lunar", "catppuccin-latte", "catppuccin-frappe", "catppuccin-macchiato", "catppuccin-mocha", "tokyo-night", "kanagawa" },
            ["Ziro"] = new[] { "blue-dark", "blue-light", "gray-dark", "gray-light", "green-dark", "green-light", "orange-dark", "orange-light", "purple-dark", "purple-light", "red-dark", "red-light", "rose-pine", "rose-pine-moon", "rose-pine-dawn", "tokyo-night" },
            ["text"] = new[] { "Spotify", "Spicetify", "CatppuccinMocha", "CatppuccinMacchiato", "CatppuccinLatte", "Dracula", "Gruvbox", "Kanagawa", "Nord", "Rigel", "RosePine", "RosePineMoon", "RosePineDawn", "Solarized", "TokyoNight", "TokyoNightStorm", "ForestGreen", "EverforestDarkHard", "EverforestDarkMedium", "EverforestDarkSoft" },
            ["StarryNight"] = new[] { "Base", "Cotton-candy", "Forest", "Galaxy", "Orange", "Sky", "Sunrise" },
            ["Turntable"] = new[] { "turntable" },
            ["Blackout"] = new[] { "def" },
            ["Blossom"] = new[] { "dark" },
            ["BurntSienna"] = new[] { "Base" },
            ["Default"] = new[] { "Ocean" },
            ["Dreary"] = new[] { "Psycho", "Deeper", "BIB", "Mono", "Golden", "Graytone-Blue" },
            ["Flow"] = new[] { "Pink", "Green", "Silver", "Violet", "Ocean" },
            ["Matte"] = new[] { "matte", "periwinkle", "periwinkle-dark", "porcelain", "rose-pine-moon", "gray-dark1", "gray-dark2", "gray-dark3", "gray", "gray-light" },
            ["Nightlight"] = new[] { "Nightlight Colors" },
            ["Onepunch"] = new[] { "dark", "light", "legacy" },
            ["SharkBlue"] = new[] { "Base" },
            // Community themes — downloaded from individual GitHub repos
            ["Catppuccin"] = new[] { "mocha", "macchiato", "frappe", "latte" },
            ["Comfy"] = new[] { "Comfy", "Mono", "Chromatic" },
            ["Bloom"] = new[] { "dark", "light", "darkMono", "darkGreen", "coffee", "comfy", "violet" },
            ["Lucid"] = new[] { "dark", "light", "dark-green", "coffee", "comfy", "dark-fluent", "greenland", "biscuit", "macos", "rosepine", "dracula", "dracula-pro" },
            ["Hazy"] = new[] { "dark", "light" }
        };

    public static IReadOnlyList<OptionDefinition> OptionDefinitions { get; } = new ReadOnlyCollection<OptionDefinition>(new[]
    {
        new OptionDefinition(nameof(InstallConfiguration.CleanInstall), Strings.Option_CleanInstall_Title, Strings.Option_CleanInstall_Description, "Install"),
        new OptionDefinition(nameof(InstallConfiguration.LaunchAfter), Strings.Option_LaunchAfter_Title, Strings.Option_LaunchAfter_Description, "Install"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_NewTheme), Strings.Option_SpotX_NewTheme_Title, Strings.Option_SpotX_NewTheme_Description, "Core"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_PodcastsOff), Strings.Option_SpotX_PodcastsOff_Title, Strings.Option_SpotX_PodcastsOff_Description, "Core"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_BlockUpdate), Strings.Option_SpotX_BlockUpdate_Title, Strings.Option_SpotX_BlockUpdate_Description, "Core"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_AdSectionsOff), Strings.Option_SpotX_AdSectionsOff_Title, Strings.Option_SpotX_AdSectionsOff_Description, "Core"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_Premium), Strings.Option_SpotX_Premium_Title, Strings.Option_SpotX_Premium_Description, "Core"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_DisableStartup), Strings.Option_SpotX_DisableStartup_Title, Strings.Option_SpotX_DisableStartup_Description, "Core"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_NoShortcut), Strings.Option_SpotX_NoShortcut_Title, Strings.Option_SpotX_NoShortcut_Description, "Core"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_StartSpoti), Strings.Option_SpotX_StartSpoti_Title, Strings.Option_SpotX_StartSpoti_Description, "Core"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_LyricsEnabled), Strings.Option_SpotX_LyricsEnabled_Title, Strings.Option_SpotX_LyricsEnabled_Description, "Interface"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_TopSearch), Strings.Option_SpotX_TopSearch_Title, Strings.Option_SpotX_TopSearch_Description, "Interface"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_RightSidebarOff), Strings.Option_SpotX_RightSidebarOff_Title, Strings.Option_SpotX_RightSidebarOff_Description, "Interface"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_RightSidebarClr), Strings.Option_SpotX_RightSidebarClr_Title, Strings.Option_SpotX_RightSidebarClr_Description, "Interface"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_CanvasHomeOff), Strings.Option_SpotX_CanvasHomeOff_Title, Strings.Option_SpotX_CanvasHomeOff_Description, "Interface"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_HomeSubOff), Strings.Option_SpotX_HomeSubOff_Title, Strings.Option_SpotX_HomeSubOff_Description, "Interface"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_OldLyrics), Strings.Option_SpotX_OldLyrics_Title, Strings.Option_SpotX_OldLyrics_Description, "Interface"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_HideColIconOff), Strings.Option_SpotX_HideColIconOff_Title, Strings.Option_SpotX_HideColIconOff_Description, "Interface"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_Plus), Strings.Option_SpotX_Plus_Title, Strings.Option_SpotX_Plus_Description, "Advanced"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_NewFullscreen), Strings.Option_SpotX_NewFullscreen_Title, Strings.Option_SpotX_NewFullscreen_Description, "Advanced"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_FunnyProgress), Strings.Option_SpotX_FunnyProgress_Title, Strings.Option_SpotX_FunnyProgress_Description, "Advanced"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_ExpSpotify), Strings.Option_SpotX_ExpSpotify_Title, Strings.Option_SpotX_ExpSpotify_Description, "Advanced"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_LyricsBlock), Strings.Option_SpotX_LyricsBlock_Title, Strings.Option_SpotX_LyricsBlock_Description, "Advanced"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_SendVersionOff), Strings.Option_SpotX_SendVersionOff_Title, Strings.Option_SpotX_SendVersionOff_Description, "Advanced"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_DevTools), Strings.Option_SpotX_DevTools_Title, Strings.Option_SpotX_DevTools_Description, "Advanced"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_Mirror), Strings.Option_SpotX_Mirror_Title, Strings.Option_SpotX_Mirror_Description, "Advanced"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_ConfirmUninstall), Strings.Option_SpotX_ConfirmUninstall_Title, Strings.Option_SpotX_ConfirmUninstall_Description, "Advanced"),
        new OptionDefinition(nameof(InstallConfiguration.Spicetify_Marketplace), Strings.Option_Spicetify_Marketplace_Title, Strings.Option_Spicetify_Marketplace_Description, "Experience")
    });

    public static IReadOnlyList<ExtensionDefinition> ExtensionDefinitions { get; } = new ReadOnlyCollection<ExtensionDefinition>(new[]
    {
        new ExtensionDefinition("fullAppDisplay.js", Strings.Extension_fullAppDisplay_Title, Strings.Extension_fullAppDisplay_Description),
        new ExtensionDefinition("shuffle+.js", Strings.Extension_shuffle_plus_Title, Strings.Extension_shuffle_plus_Description),
        new ExtensionDefinition("trashbin.js", Strings.Extension_trashbin_Title, Strings.Extension_trashbin_Description),
        new ExtensionDefinition("keyboardShortcut.js", Strings.Extension_keyboardShortcut_Title, Strings.Extension_keyboardShortcut_Description),
        new ExtensionDefinition("bookmark.js", Strings.Extension_bookmark_Title, Strings.Extension_bookmark_Description),
        new ExtensionDefinition("loopyLoop.js", Strings.Extension_loopyLoop_Title, Strings.Extension_loopyLoop_Description),
        new ExtensionDefinition("popupLyrics.js", Strings.Extension_popupLyrics_Title, Strings.Extension_popupLyrics_Description),
        new ExtensionDefinition("autoSkipVideo.js", Strings.Extension_autoSkipVideo_Title, Strings.Extension_autoSkipVideo_Description),
        new ExtensionDefinition("autoSkipExplicit.js", Strings.Extension_autoSkipExplicit_Title, Strings.Extension_autoSkipExplicit_Description),
        new ExtensionDefinition("webnowplaying.js", Strings.Extension_webnowplaying_Title, Strings.Extension_webnowplaying_Description),
        // Community extensions — downloaded from GitHub during install
        new ExtensionDefinition("hidePodcasts.js", Strings.Extension_hidePodcasts_Title, Strings.Extension_hidePodcasts_Description),
        new ExtensionDefinition("beautiful-lyrics.mjs", Strings.Extension_beautiful_lyrics_Title, Strings.Extension_beautiful_lyrics_Description),
        new ExtensionDefinition("playlist-icons.js", Strings.Extension_playlist_icons_Title, Strings.Extension_playlist_icons_Description),
        new ExtensionDefinition("volumePercentage.js", Strings.Extension_volumePercentage_Title, Strings.Extension_volumePercentage_Description),
        new ExtensionDefinition("adblock.js", Strings.Extension_adblock_Title, Strings.Extension_adblock_Description)
    });

    public static IReadOnlyList<CustomAppDefinition> CustomAppDefinitions { get; } = new ReadOnlyCollection<CustomAppDefinition>(new[]
    {
        new CustomAppDefinition(
            "stats",
            "Stats",
            "Detailed listening statistics with top tracks, artists, genres, library charts, and optional Last.fm-backed views.")
    });

    private static readonly IReadOnlyDictionary<string, string> ExtensionAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["beautifulLyrics.js"] = "beautiful-lyrics.mjs",
        ["playlistIcons.js"] = "playlist-icons.js"
    };

    public static IReadOnlyList<MaintenanceActionDefinition> MaintenanceActions { get; } = new ReadOnlyCollection<MaintenanceActionDefinition>(new[]
    {
        new MaintenanceActionDefinition("CheckUpdates", Strings.Maintenance_CheckUpdates_Title, Strings.Maintenance_CheckUpdates_Description, Strings.Maintenance_CheckUpdates_ButtonText),
        new MaintenanceActionDefinition("Reapply", Strings.Maintenance_Reapply_Title, Strings.Maintenance_Reapply_Description, Strings.Maintenance_Reapply_ButtonText),
        new MaintenanceActionDefinition("RepairMarketplace", Strings.Maintenance_RepairMarketplace_Title, Strings.Maintenance_RepairMarketplace_Description, Strings.Maintenance_RepairMarketplace_ButtonText),
        new MaintenanceActionDefinition("OpenMarketplace", Strings.Maintenance_OpenMarketplace_Title, Strings.Maintenance_OpenMarketplace_Description, Strings.Maintenance_OpenMarketplace_ButtonText),
        new MaintenanceActionDefinition("SafeMode", Strings.Maintenance_SafeMode_Title, Strings.Maintenance_SafeMode_Description, Strings.Maintenance_SafeMode_ButtonText),
        new MaintenanceActionDefinition("CreateBackup", Strings.Maintenance_CreateBackup_Title, Strings.Maintenance_CreateBackup_Description, Strings.Maintenance_CreateBackup_ButtonText),
        new MaintenanceActionDefinition("RestoreBackup", Strings.Maintenance_RestoreBackup_Title, Strings.Maintenance_RestoreBackup_Description, Strings.Maintenance_RestoreBackup_ButtonText),
        new MaintenanceActionDefinition("RestoreVanilla", Strings.Maintenance_RestoreVanilla_Title, Strings.Maintenance_RestoreVanilla_Description, Strings.Maintenance_RestoreVanilla_ButtonText),
        new MaintenanceActionDefinition("UninstallSpicetify", Strings.Maintenance_UninstallSpicetify_Title, Strings.Maintenance_UninstallSpicetify_Description, Strings.Maintenance_UninstallSpicetify_ButtonText, true),
        new MaintenanceActionDefinition("FullReset", Strings.Maintenance_FullReset_Title, Strings.Maintenance_FullReset_Description, Strings.Maintenance_FullReset_ButtonText, true),
        new MaintenanceActionDefinition("RemoveSelfData", Strings.Maintenance_RemoveSelfData_Title, Strings.Maintenance_RemoveSelfData_Description, Strings.Maintenance_RemoveSelfData_ButtonText, true)
    });

    public static IReadOnlyList<string> RecommendedHighlights { get; } = new ReadOnlyCollection<string>(new[]
    {
        Strings.RecommendedHighlight_1,
        Strings.RecommendedHighlight_2,
        Strings.RecommendedHighlight_3,
        Strings.RecommendedHighlight_4
    });

    /// <summary>
    /// Evaluates whether a Spotify version entry is compatible with the host
    /// architecture. Returns null when compatible, or a human-readable warning
    /// string when the combination is mismatched.
    /// </summary>
    public static string? CheckArchitectureCompatibility(SpotifyVersionEntry entry, string hostArchitecture)
    {
        if (entry is null)
        {
            return null;
        }

        var arch = entry.Architecture;
        var host = (hostArchitecture ?? "Unknown").Trim();

        // "any" and "auto" entries are always compatible.
        if (string.Equals(arch, "any", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entry.Id, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // x86-only build selected on a 64-bit or ARM64 host.
        if (string.Equals(arch, "x86", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(host, "X86", StringComparison.OrdinalIgnoreCase))
        {
            return $"This is a 32-bit Spotify build intended for x86 Windows. Your host is {host}. SpotX and Spicetify patches may not apply correctly.";
        }

        // x64-only build selected on an x86 host.
        if (string.Equals(arch, "x64", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(host, "X86", StringComparison.OrdinalIgnoreCase))
        {
            return $"This is a 64-bit Spotify build, but your host is {host}. Choose the x86 build or Auto instead.";
        }

        // x64 build running under emulation on ARM64.
        if (string.Equals(arch, "x64", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(host, "ARM64", StringComparison.OrdinalIgnoreCase))
        {
            return "This x64 Spotify build will run under emulation on ARM64. SpotX and Spicetify patches are untested in this configuration.";
        }

        // Legacy OS build selected on a modern Windows host.
        if (string.Equals(arch, "legacy-os", StringComparison.OrdinalIgnoreCase))
        {
            var osVersion = Environment.OSVersion.Version;
            // Windows 10 is 10.0; Windows 7 is 6.1, Windows 8.1 is 6.3.
            if (osVersion.Major >= 10)
            {
                return "This Spotify build targets Windows 7/8.1 and lacks features available in modern builds. Use Auto or a current pinned version on Windows 10+.";
            }
        }

        return null;
    }

    public static IReadOnlyList<string> CheckInstalledSpotifyCompatibility(string? installedSpotifyVersion)
    {
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(installedSpotifyVersion))
        {
            return warnings;
        }

        var installed = NormalizeSpotifyVersion(installedSpotifyVersion);
        var maxTested = NormalizeSpotifyVersion(SpicetifyWindowsMaxTestedSpotify);
        if (installed is not null && maxTested is not null && installed > maxTested)
        {
            warnings.Add(
                $"Installed Spotify {installedSpotifyVersion} is newer than Spicetify CLI's max-tested version ({SpicetifyWindowsMaxTestedSpotify}). " +
                "Themes and extensions may not apply correctly.");
        }

        return warnings;
    }

    private static Version? NormalizeSpotifyVersion(string version)
    {
        var parts = version.Split('.');
        if (parts.Length >= 3 && int.TryParse(parts[0], out var major) &&
            int.TryParse(parts[1], out var minor) && int.TryParse(parts[2], out var build))
        {
            return new Version(major, minor, build);
        }

        return null;
    }

    public static InstallConfiguration CreateRecommendedConfiguration() => new();

    public static InstallConfiguration NormalizeConfiguration(InstallConfiguration? source)
    {
        var normalized = CreateRecommendedConfiguration().Clone();
        if (source is null)
        {
            return normalized;
        }

        if (source.ConfigSchemaVersion > CurrentConfigSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Saved config schema version {source.ConfigSchemaVersion} is newer than this LibreSpot build supports ({CurrentConfigSchemaVersion}).");
        }

        normalized.ConfigSchemaVersion = CurrentConfigSchemaVersion;
        normalized.Mode = source.Mode is "Easy" or "Custom" ? source.Mode : normalized.Mode;
        normalized.UiCulture = NormalizeUiCulture(source.UiCulture);
        normalized.CleanInstall = source.CleanInstall;
        normalized.LaunchAfter = source.LaunchAfter;

        normalized.SpotX_NewTheme = source.SpotX_NewTheme;
        normalized.SpotX_PodcastsOff = source.SpotX_PodcastsOff;
        normalized.SpotX_BlockUpdate = source.SpotX_BlockUpdate;
        normalized.SpotX_AdSectionsOff = source.SpotX_AdSectionsOff;
        normalized.SpotX_Premium = source.SpotX_Premium;
        normalized.SpotX_LyricsEnabled = source.SpotX_LyricsEnabled;
        normalized.SpotX_TopSearch = source.SpotX_TopSearch;
        normalized.SpotX_RightSidebarOff = source.SpotX_RightSidebarOff;
        normalized.SpotX_RightSidebarClr = source.SpotX_RightSidebarClr && !source.SpotX_RightSidebarOff;
        normalized.SpotX_CanvasHomeOff = source.SpotX_CanvasHomeOff;
        normalized.SpotX_HomeSubOff = source.SpotX_HomeSubOff;
        normalized.SpotX_DisableStartup = source.SpotX_DisableStartup;
        normalized.SpotX_NoShortcut = source.SpotX_NoShortcut;
        normalized.SpotX_CacheLimit = Math.Clamp(source.SpotX_CacheLimit, 0, 50_000);
        normalized.SpotX_Plus = source.SpotX_Plus;
        normalized.SpotX_NewFullscreen = source.SpotX_NewFullscreen;
        normalized.SpotX_FunnyProgress = source.SpotX_FunnyProgress;
        normalized.SpotX_ExpSpotify = source.SpotX_ExpSpotify;
        normalized.SpotX_LyricsBlock = source.SpotX_LyricsEnabled && source.SpotX_LyricsBlock;
        normalized.SpotX_OldLyrics = source.SpotX_LyricsEnabled && source.SpotX_OldLyrics && !source.SpotX_LyricsBlock;
        normalized.SpotX_HideColIconOff = source.SpotX_HideColIconOff;
        normalized.SpotX_SendVersionOff = source.SpotX_SendVersionOff;
        normalized.SpotX_StartSpoti = source.SpotX_StartSpoti;
        normalized.SpotX_DevTools = source.SpotX_DevTools;
        normalized.SpotX_Mirror = source.SpotX_Mirror;
        normalized.SpotX_ConfirmUninstall = source.SpotX_ConfirmUninstall;
        normalized.SpotX_CustomPatchesEnabled = source.SpotX_CustomPatchesEnabled;
        normalized.SpotX_CustomPatchesJson = TruncateCustomPatchesJson(source.SpotX_CustomPatchesJson);
        if (!string.IsNullOrWhiteSpace(normalized.SpotX_CustomPatchesJson))
        {
            normalized.SpotX_CustomPatchesSourceUrl = NormalizeCustomPatchSourceUrl(source.SpotX_CustomPatchesSourceUrl);
            normalized.SpotX_CustomPatchesFetchedAtUtc = source.SpotX_CustomPatchesFetchedAtUtc?.ToUniversalTime();
            normalized.SpotX_CustomPatchesSourceByteCount = source.SpotX_CustomPatchesSourceByteCount is > 0 and <= 64 * 1024
                ? source.SpotX_CustomPatchesSourceByteCount
                : 0;
            normalized.SpotX_CustomPatchesSourceSha256 = NormalizeSha256(source.SpotX_CustomPatchesSourceSha256);
        }

        var rawDm = (source.SpotX_DownloadMethod ?? string.Empty).Trim().ToLowerInvariant();
        normalized.SpotX_DownloadMethod = rawDm is "curl" or "webclient" ? rawDm : "";

        var allowedLanguages = new HashSet<string>(StringComparer.Ordinal)
        {
            "en","ru","de","fr","es","pt","pt-BR","it","nl","pl","sv","no","da","fi",
            "ja","ko","zh-CN","zh-TW","ar","tr","cs","hu","ro","uk","id","th","vi"
        };
        var rawLang = (source.SpotX_Language ?? string.Empty).Trim();
        normalized.SpotX_Language = allowedLanguages.Contains(rawLang) ? rawLang : "";

        var rawVersionId = (source.SpotX_SpotifyVersionId ?? string.Empty).Trim();
        var canonicalVersionId = SpotifyVersionManifest.FirstOrDefault(entry => string.Equals(entry.Id, rawVersionId, StringComparison.OrdinalIgnoreCase))?.Id;
        normalized.SpotX_SpotifyVersionId = !string.IsNullOrWhiteSpace(canonicalVersionId)
            ? canonicalVersionId
            : "auto";

        normalized.SpotX_LyricsTheme = !string.IsNullOrWhiteSpace(source.SpotX_LyricsTheme) && LyricsThemes.Contains(source.SpotX_LyricsTheme)
            ? source.SpotX_LyricsTheme
            : normalized.SpotX_LyricsTheme;

        normalized.Spicetify_Theme = !string.IsNullOrWhiteSpace(source.Spicetify_Theme) && ThemeSchemes.ContainsKey(source.Spicetify_Theme)
            ? source.Spicetify_Theme
            : normalized.Spicetify_Theme;

        // normalized.Spicetify_Theme is guaranteed to be a ThemeSchemes key above, but use
        // TryGetValue defensively so a future rename of the default theme can't turn this
        // into a KeyNotFoundException.
        var validSchemes = ThemeSchemes.TryGetValue(normalized.Spicetify_Theme, out var schemes)
            ? schemes
            : ThemeSchemes["(None - Marketplace Only)"];

        normalized.Spicetify_Scheme = validSchemes.Contains(source.Spicetify_Scheme)
            ? source.Spicetify_Scheme
            : validSchemes.First();

        normalized.Spicetify_Marketplace = source.Spicetify_Marketplace;
        normalized.AutoReapply_Enabled = source.AutoReapply_Enabled;
        normalized.RiskAcknowledged = source.RiskAcknowledged;

        var validExtensions = ExtensionDefinitions.Select(def => def.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        normalized.Spicetify_Extensions = (source.Spicetify_Extensions ?? [])
            .Select(item =>
            {
                var name = item ?? string.Empty;
                return ExtensionAliases.TryGetValue(name, out var currentName) ? currentName : name;
            })
            .Where(item => !string.IsNullOrWhiteSpace(item) && validExtensions.Contains(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var validCustomApps = CustomAppDefinitions.Select(def => def.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        normalized.Spicetify_CustomApps = (source.Spicetify_CustomApps ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item) && validCustomApps.Contains(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return normalized;
    }

    private static string TruncateCustomPatchesJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        var trimmed = json.Trim();
        const int maxChars = 64 * 1024;
        return trimmed.Length <= maxChars ? trimmed : trimmed[..maxChars];
    }

    private static string NormalizeCustomPatchSourceUrl(string? url)
    {
        var trimmed = (url ?? string.Empty).Trim();
        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
               string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? uri.ToString()
            : string.Empty;
    }

    private static string NormalizeSha256(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim().ToLowerInvariant();
        return trimmed.Length == 64 && trimmed.All(ch => char.IsAsciiHexDigit(ch))
            ? trimmed
            : string.Empty;
    }
}
