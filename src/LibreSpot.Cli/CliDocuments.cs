using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;

namespace LibreSpot.Cli;

public sealed record StatusDocument(
    int SchemaVersion,
    string ProductVersion,
    DateTimeOffset GeneratedAtUtc,
    string ConfigPath,
    string HostArchitecture,
    string ProcessArchitecture,
    string StatusTitle,
    string StatusDetail,
    string IssueSummary,
    bool SpotifyInstalled,
    bool SpicetifyInstalled,
    bool MarketplaceReady,
    bool MarketplaceLikelyVisible,
    MarketplaceVisibilityDocument? MarketplaceVisibility,
    PatcherOwnershipDocument PatcherOwnership,
    AssetCacheDocument AssetCache,
    bool AutoReapplyTaskRegistered,
    int BackupCount,
    DateTimeOffset? LastPatchTimeUtc,
    string? LastWatcherOutcome,
    IReadOnlyList<string> IssueIds,
    IReadOnlyList<string> RecommendedRepairIds,
    IReadOnlyList<CommunityAssetDocument> CommunityAssets,
    IReadOnlyList<UpstreamDependencyDocument> UpstreamDependencies,
    IReadOnlyList<ComponentDocument> Components);

public sealed record PatcherOwnershipDocument(
    string Ownership,
    string Summary,
    string Recommendation,
    bool HasForeignState,
    IReadOnlyList<PatcherFootprintDocument> Footprints)
{
    public static PatcherOwnershipDocument From(PatcherOwnershipReport report) =>
        new(
            report.Ownership,
            report.Summary,
            report.Recommendation,
            report.HasForeignState,
            report.Footprints.Select(PatcherFootprintDocument.From).ToArray());
}

public sealed record PatcherFootprintDocument(
    string Id,
    string Name,
    string Confidence,
    string Ownership,
    IReadOnlyList<string> EvidencePaths,
    string Recommendation)
{
    public static PatcherFootprintDocument From(PatcherFootprint footprint) =>
        new(
            footprint.Id,
            footprint.Name,
            footprint.Confidence,
            footprint.Ownership,
            footprint.EvidencePaths,
            footprint.Recommendation);
}

public sealed record AssetCacheDocument(
    int EntryCount,
    int PresentCount,
    int MissingCount,
    int CorruptCount,
    int UnindexedCount,
    int StaleCount,
    long TotalBytes,
    string CacheDirectory,
    string IndexPath,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<AssetCacheEntryDocument> Entries)
{
    public static AssetCacheDocument From(AssetCacheInventoryReport report) =>
        new(
            report.EntryCount,
            report.PresentCount,
            report.MissingCount,
            report.CorruptCount,
            report.UnindexedCount,
            report.StaleCount,
            report.TotalBytes,
            report.CacheDirectory,
            report.IndexPath,
            report.GeneratedAtUtc,
            report.Entries.Select(AssetCacheEntryDocument.From).ToArray());
}

public sealed record AssetCacheEntryDocument(
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
    string Evidence)
{
    public static AssetCacheEntryDocument From(AssetCacheEntryState entry) =>
        new(
            entry.Sha256,
            entry.Label,
            entry.SourceUrl,
            entry.ByteSize,
            entry.FirstSeenAtUtc,
            entry.LastUsedAtUtc,
            entry.LastVerifiedAtUtc,
            entry.Status,
            entry.Path,
            entry.FilePresent,
            entry.Evidence);
}

public sealed record MarketplaceVisibilityDocument(
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
    DateTimeOffset? LastObservedAtUtc)
{
    public static MarketplaceVisibilityDocument From(MarketplaceVisibilityEvidence evidence) =>
        new(
            evidence.SchemaVersion,
            evidence.GeneratedAtUtc,
            evidence.Source,
            evidence.FilesPresent,
            evidence.Registered,
            evidence.LikelyVisible,
            evidence.MarketplaceStatus,
            evidence.MarketplacePath,
            evidence.ManifestVersion,
            evidence.ApplyStage,
            evidence.ApplySucceeded,
            evidence.ApplyMessage,
            evidence.ApplyCompletedAtUtc,
            evidence.OpenUriSucceeded,
            evidence.OpenUriMessage,
            evidence.OpenUriRequestedAtUtc,
            evidence.SpotifyRunningAfterOpen,
            evidence.LastObservedSpotifySession,
            evidence.LastObservedAtUtc);
}

public sealed record CommunityAssetDocument(
    string Id,
    string Kind,
    string Name,
    string SourceUrl,
    string? ReleaseNotesUrl,
    DateTimeOffset? LastVerifiedAtUtc,
    string GitRepository,
    string GitReference,
    string PinnedCommit,
    string? PinnedHash,
    string? LatestCommit,
    string DriftState,
    string MetadataSource,
    DateTimeOffset CheckedAtUtc,
    double? CacheAgeSeconds,
    bool IsDegraded,
    string FreshnessStatus,
    string License,
    string SupportState,
    string FallbackBehavior,
    string NetworkBehavior,
    string? NetworkDetail,
    bool RequiresTrustReview,
    string Evidence)
{
    public static CommunityAssetDocument From(CommunityAssetState asset) =>
        new(
            asset.Id,
            asset.Kind,
            asset.Name,
            asset.SourceUrl,
            asset.ReleaseNotesUrl,
            asset.LastVerifiedAtUtc,
            asset.GitRepository,
            asset.GitReference,
            asset.PinnedCommit,
            asset.PinnedHash,
            asset.LatestCommit,
            asset.DriftState,
            asset.MetadataSource,
            asset.CheckedAtUtc,
            asset.CacheAge?.TotalSeconds,
            asset.IsDegraded,
            asset.FreshnessStatus,
            asset.License,
            asset.SupportState,
            asset.FallbackBehavior,
            asset.NetworkBehavior,
            asset.NetworkDetail,
            asset.RequiresTrustReview,
            asset.Evidence);
}

public sealed record UpstreamDependencyDocument(
    string Id,
    string Name,
    string SourceUrl,
    string? ReleaseNotesUrl,
    DateTimeOffset? LastVerifiedAtUtc,
    string PinnedValue,
    string CurrentValue,
    string? LatestValue,
    string DriftState,
    string MetadataSource,
    DateTimeOffset CheckedAtUtc,
    double? CacheAgeSeconds,
    bool IsDegraded,
    string FreshnessStatus,
    string Evidence)
{
    public static UpstreamDependencyDocument From(UpstreamDependencyState dependency) =>
        new(
            dependency.Id,
            dependency.Name,
            dependency.SourceUrl,
            dependency.ReleaseNotesUrl,
            dependency.LastVerifiedAtUtc,
            dependency.PinnedValue,
            dependency.CurrentValue,
            dependency.LatestValue,
            dependency.DriftState,
            dependency.MetadataSource,
            dependency.CheckedAtUtc,
            dependency.CacheAge?.TotalSeconds,
            dependency.IsDegraded,
            dependency.FreshnessStatus,
            dependency.Evidence);
}

public sealed record ComponentDocument(
    string Id,
    string Name,
    string Status,
    string Severity,
    string? DetectedVersion,
    string? Path,
    DateTimeOffset? LastChangedUtc,
    string Evidence,
    IReadOnlyList<string> RecommendedActionIds)
{
    public static ComponentDocument From(StackHealthComponent component) =>
        new(
            component.Id,
            component.Name,
            component.Status,
            component.Severity,
            component.DetectedVersion,
            component.Path,
            component.LastChanged.HasValue ? new DateTimeOffset(component.LastChanged.Value.ToUniversalTime()) : null,
            component.Evidence,
            component.RecommendedActionIds);
}

public sealed record DetectionDocument(
    int SchemaVersion,
    string ProductVersion,
    DateTimeOffset GeneratedAtUtc,
    string ConfigPath,
    string State,
    int ExitCode,
    string Summary,
    IReadOnlyList<string> IssueIds,
    IReadOnlyList<string> RecommendedRepairIds);

public sealed record ValidationDocument(
    int SchemaVersion,
    string AnswerFile,
    bool Valid,
    IReadOnlyList<ValidationErrorDocument> Errors);

public sealed record ValidationErrorDocument(string Path, string Message);

public sealed record UndoDocument(
    int SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string SourceOperationId,
    string TokenKind,
    bool DryRun,
    bool Allowed,
    bool AlreadyUndone,
    bool Changed,
    string Status,
    string Reason,
    string? UndoOperationId);

public sealed record PlanDocument(
    int SchemaVersion,
    string ProductVersion,
    DateTimeOffset GeneratedAtUtc,
    Guid OperationId,
    string Operation,
    bool DryRun,
    bool Mutates,
    string? CorrelationId,
    string? AnswerFile,
    string? Profile,
    IReadOnlyList<PlanStepDocument> Steps);

public sealed record PlanStepDocument(
    string Id,
    string Title,
    bool RequiresAdmin,
    bool Mutates,
    string Target,
    string Detail);

public sealed record VersionDocument(
    int SchemaVersion,
    string ProductVersion,
    string AssemblyVersion,
    DateTimeOffset GeneratedAtUtc,
    string FrameworkDescription,
    string RuntimeIdentifier,
    string ProcessArchitecture,
    string OsDescription,
    DependencyPinsDocument Dependencies);

public sealed record DependencyPinsDocument(
    SpotXPinDocument SpotX,
    SpicetifyPinDocument SpicetifyCli,
    string MarketplaceVersion,
    string ThemesCommit);

public sealed record SpotXPinDocument(
    string Version,
    string Commit,
    string SpotifyTargetId,
    string SpotifyTargetVersion);

public sealed record SpicetifyPinDocument(
    string Version,
    string WindowsMinTestedSpotify,
    string WindowsMaxTestedSpotify);

public sealed record NdjsonLogLine(
    int SchemaVersion,
    string EventId,
    DateTimeOffset Timestamp,
    string Level,
    string Verb,
    Guid OperationId,
    string? CorrelationId,
    string Component,
    string? Target,
    string Message,
    object Payload,
    int? ExitCode);
