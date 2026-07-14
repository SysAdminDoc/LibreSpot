using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LibreSpot.Desktop.Models;

namespace LibreSpot.Desktop.Services;

public sealed record SupportBundleOptions(
    bool IncludeOperationJournal = true,
    bool IncludeLogs = true,
    bool IncludeCrashReports = true,
    SupportBundleRunContext? CurrentRun = null);

public sealed record SupportBundleRunContext(
    string Title,
    string Status,
    string Step,
    string Outcome,
    string? BackendAction,
    string? BackendErrorCode,
    string? BackendErrorMessage,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CapturedAt,
    IReadOnlyList<string> LogLines);

public sealed record SupportBundlePreviewEntry(
    string Id,
    string Title,
    string Detail,
    int FileCount,
    long EstimatedBytes,
    bool IsRequired,
    bool IsSelected);

public sealed record SupportBundlePreview(
    IReadOnlyList<SupportBundlePreviewEntry> Entries,
    long EstimatedBytes,
    IReadOnlyList<string> RedactionRules)
{
    public int SelectedFileCount => Entries.Where(entry => entry.IsSelected).Sum(entry => entry.FileCount);
}

public sealed record SupportBundleResult(string Path, int EntryCount, long BytesWritten);

public sealed class SupportBundleService
{
    private const int MaxOperationLines = 240;
    private const int MaxLogLines = 500;
    private const int MaxCrashLines = 900;
    private const int MaxRollingLogFiles = 3;
    private const int MaxCrashFiles = 3;
    private const long MetadataEstimateBytes = 16 * 1024;
    private static readonly Encoding StrictUtf8NoBom = new UTF8Encoding(false, true);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions ConfigJsonOptions = new()
    {
        PropertyNamingPolicy = null
    };

    private readonly string _configDirectory;
    private readonly string _rollingLogDirectory;
    private readonly string _crashDirectory;
    private readonly SupportBundleRedactor _redactor;

    public SupportBundleService(
        string configDirectory,
        string? rollingLogDirectory = null,
        string? crashDirectory = null)
    {
        _configDirectory = Path.GetFullPath(configDirectory);
        _rollingLogDirectory = string.IsNullOrWhiteSpace(rollingLogDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LibreSpot", "logs")
            : Path.GetFullPath(rollingLogDirectory);
        _crashDirectory = string.IsNullOrWhiteSpace(crashDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LibreSpot", "crashes")
            : Path.GetFullPath(crashDirectory);
        _redactor = new SupportBundleRedactor(_configDirectory);
    }

    public IReadOnlyList<string> RedactionRules => SupportBundleRedactor.RuleDescriptions;

    public string RedactText(string text) => _redactor.Redact(text);

    public SupportBundlePreview CreatePreview(EnvironmentSnapshot snapshot, SupportBundleOptions options)
    {
        var entries = new[]
        {
            new SupportBundlePreviewEntry(
                "health",
                "Health report",
                "Required redacted snapshot, app/runtime versions, and catalog pins.",
                3,
                EstimateHealthBytes(snapshot),
                true,
                true),
            new SupportBundlePreviewEntry(
                "operation",
                "Operation journal",
                "Recent install and watcher state from the LibreSpot profile.",
                CountExisting(OperationFiles()),
                EstimateFiles(OperationFiles(), MaxOperationLines),
                false,
                options.IncludeOperationJournal),
            new SupportBundlePreviewEntry(
                "logs",
                "Logs",
                $"Selected install, watcher, and desktop logs; newest {MaxRollingLogFiles} rolling desktop logs.",
                CountExisting(LogFiles()),
                EstimateFiles(LogFiles(), MaxLogLines),
                false,
                options.IncludeLogs),
            new SupportBundlePreviewEntry(
                "crashes",
                "Crash reports",
                $"Selected newest crash report windows; newest {MaxCrashFiles} reports.",
                CountExisting(CrashFiles()),
                EstimateFiles(CrashFiles(), MaxCrashLines),
                false,
                options.IncludeCrashReports)
        };

        return new SupportBundlePreview(
            entries,
            entries.Where(entry => entry.IsSelected).Sum(entry => entry.EstimatedBytes),
            RedactionRules);
    }

    public async Task<SupportBundleResult> ExportAsync(
        string destinationPath,
        EnvironmentSnapshot snapshot,
        SupportBundleOptions options,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException("A destination path is required.", nameof(destinationPath));
        }

        var fullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var preview = CreatePreview(snapshot, options);
        var entryCount = 0;
        var tempDirectory = string.IsNullOrWhiteSpace(directory) ? Environment.CurrentDirectory : directory;
        var tempPath = Path.Combine(tempDirectory, $"{Path.GetFileName(fullPath)}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                AddJsonEntry(archive, "manifest.json", BuildManifest(preview, options, snapshot));
                entryCount++;
                AddJsonEntry(archive, "health/health-report.json", BuildHealthReport(snapshot));
                entryCount++;
                AddJsonEntry(archive, "health/runtime.json", BuildRuntimeReport(snapshot));
                entryCount++;

                if (options.IncludeOperationJournal)
                {
                    AddTextEntry(archive, "operation/latest-journal.txt", BuildOperationJournal());
                    entryCount++;
                }

                if (options.CurrentRun is not null)
                {
                    AddTextEntry(archive, "current-run/activity-log.txt", BuildCurrentRunLog(options.CurrentRun));
                    entryCount++;
                    AddJsonEntry(archive, "current-run/backend-result.json", BuildCurrentRunMetadata(options.CurrentRun));
                    entryCount++;
                }

                if (options.IncludeLogs)
                {
                    foreach (var file in LogFiles())
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        if (TryAddRedactedFileWindow(archive, "logs", file, MaxLogLines))
                        {
                            entryCount++;
                        }
                    }
                }

                if (options.IncludeCrashReports)
                {
                    foreach (var file in CrashFiles())
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        if (TryAddRedactedFileWindow(archive, "crashes", file, MaxCrashLines))
                        {
                            entryCount++;
                        }
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            File.Move(tempPath, fullPath, overwrite: true);
        }
        catch
        {
            try { File.Delete(tempPath); } catch { }
            throw;
        }

        var writtenBytes = new FileInfo(fullPath).Length;
        return new SupportBundleResult(fullPath, entryCount, writtenBytes);
    }

    public string CreateDefaultBundlePath()
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        return Path.Combine(_configDirectory, $"LibreSpot-support-{stamp}.zip");
    }

    public string CreateDefaultFailureBundlePath()
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        return Path.Combine(_configDirectory, $"LibreSpot-failure-{stamp}.zip");
    }

    private object BuildManifest(SupportBundlePreview preview, SupportBundleOptions options, EnvironmentSnapshot snapshot) =>
        new
        {
            schemaVersion = 1,
            generatedAt = DateTimeOffset.Now,
            networkUpload = "none",
            options = new
            {
                options.IncludeOperationJournal,
                options.IncludeLogs,
                options.IncludeCrashReports,
                includeCurrentRun = options.CurrentRun is not null
            },
            selectedFileCount = preview.SelectedFileCount,
            estimatedBytes = preview.EstimatedBytes,
            healthStatus = snapshot.HealthReport.StatusTitle,
            currentRun = options.CurrentRun is null
                ? null
                : new
                {
                    outcome = options.CurrentRun.Outcome,
                    backendAction = options.CurrentRun.BackendAction,
                    backendErrorCode = options.CurrentRun.BackendErrorCode,
                    logLineCount = options.CurrentRun.LogLines.Count
                },
            entries = preview.Entries.Select(entry => new
            {
                entry.Id,
                entry.Title,
                entry.FileCount,
                entry.EstimatedBytes,
                entry.IsRequired,
                entry.IsSelected
            }),
            redactionRules = preview.RedactionRules
        };

    private object BuildHealthReport(EnvironmentSnapshot snapshot) =>
        new
        {
            snapshot.HealthReport.StatusTitle,
            snapshot.HealthReport.StatusDetail,
            snapshot.HealthReport.IssueSummary,
            snapshot.SpotifyInstalled,
            snapshot.SpicetifyInstalled,
            snapshot.MarketplaceFilesPresent,
            snapshot.MarketplaceRegistered,
            snapshot.MarketplaceReady,
            snapshot.MarketplaceLikelyVisible,
            snapshot.SavedConfigExists,
            snapshot.ConfigFolderExists,
            snapshot.AutoReapplyTaskRegistered,
            customPatchImport = BuildCustomPatchImportReport(),
            marketplaceVisibility = BuildMarketplaceVisibilityReport(snapshot.MarketplaceVisibilityEvidence),
            assetCache = BuildAssetCacheReport(snapshot.AssetCacheInventory),
            communityAssets = snapshot.CommunityAssetDriftReport.Assets.Select(asset => new
            {
                asset.Id,
                asset.Kind,
                asset.Name,
                asset.SourceUrl,
                asset.GitRepository,
                asset.GitReference,
                asset.PinnedCommit,
                asset.PinnedHash,
                asset.LatestCommit,
                asset.DriftState,
                asset.MetadataSource,
                asset.CheckedAtUtc,
                cacheAgeSeconds = asset.CacheAge?.TotalSeconds,
                asset.IsDegraded,
                asset.License,
                asset.SupportState,
                asset.FallbackBehavior,
                asset.NetworkBehavior,
                asset.NetworkDetail,
                asset.RequiresTrustReview,
                evidence = RedactNullable(asset.Evidence)
            }),
            components = snapshot.HealthReport.Components.Select(component => new
            {
                component.Id,
                component.Name,
                component.Status,
                component.Severity,
                component.DetectedVersion,
                path = RedactNullable(component.Path),
                lastChanged = component.LastChanged,
                evidence = RedactNullable(component.Evidence),
                recommendedActionIds = component.RecommendedActionIds
            })
            };

    private object BuildAssetCacheReport(AssetCacheInventoryReport report) =>
        new
        {
            report.EntryCount,
            report.PresentCount,
            report.MissingCount,
            report.CorruptCount,
            report.UnindexedCount,
            report.StaleCount,
            report.TotalBytes,
            cacheDirectory = RedactText(report.CacheDirectory),
            indexPath = RedactText(report.IndexPath),
            report.GeneratedAtUtc,
            entries = report.Entries.Select(entry => new
            {
                entry.Sha256,
                entry.Label,
                sourceUrl = RedactNullable(entry.SourceUrl),
                entry.ByteSize,
                entry.FirstSeenAtUtc,
                entry.LastUsedAtUtc,
                entry.LastVerifiedAtUtc,
                entry.Status,
                path = RedactText(entry.Path),
                entry.FilePresent,
                evidence = RedactNullable(entry.Evidence)
            })
        };

    private object? BuildMarketplaceVisibilityReport(MarketplaceVisibilityEvidence? evidence) =>
        evidence is null
            ? null
            : new
            {
                evidence.SchemaVersion,
                evidence.GeneratedAtUtc,
                evidence.Source,
                evidence.FilesPresent,
                evidence.Registered,
                evidence.LikelyVisible,
                evidence.MarketplaceStatus,
                marketplacePath = RedactNullable(evidence.MarketplacePath),
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
                evidence.LastObservedAtUtc
            };

    private object? BuildCustomPatchImportReport()
    {
        var configuration = ReadSavedConfiguration();
        if (configuration is null ||
            string.IsNullOrWhiteSpace(configuration.SpotX_CustomPatchesSourceUrl) ||
            string.IsNullOrWhiteSpace(configuration.SpotX_CustomPatchesSourceSha256))
        {
            return null;
        }

        return new
        {
            enabled = configuration.SpotX_CustomPatchesEnabled,
            sourceUrl = RedactText(configuration.SpotX_CustomPatchesSourceUrl),
            fetchedAtUtc = configuration.SpotX_CustomPatchesFetchedAtUtc,
            sourceByteCount = configuration.SpotX_CustomPatchesSourceByteCount,
            sourceSha256 = configuration.SpotX_CustomPatchesSourceSha256,
            reviewedJsonBytes = Encoding.UTF8.GetByteCount(configuration.SpotX_CustomPatchesJson ?? string.Empty)
        };
    }

    private InstallConfiguration? ReadSavedConfiguration()
    {
        var configPath = Path.Combine(_configDirectory, "config.json");
        if (!File.Exists(configPath))
        {
            return null;
        }

        try
        {
            using var stream = File.Open(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var configuration = JsonSerializer.Deserialize<InstallConfiguration>(stream, ConfigJsonOptions);
            return configuration is null ? null : AppCatalog.NormalizeConfiguration(configuration);
        }
        catch
        {
            return null;
        }
    }

    private object BuildRuntimeReport(EnvironmentSnapshot snapshot)
    {
        var spotify = snapshot.HealthReport.Components.FirstOrDefault(component => component.Id == "spotify");
        var spicetify = snapshot.HealthReport.Components.FirstOrDefault(component => component.Id == "spicetify-cli");

        return new
        {
            appVersion = typeof(SupportBundleService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? typeof(SupportBundleService).Assembly.GetName().Version?.ToString()
                ?? "unknown",
            assemblyVersion = typeof(SupportBundleService).Assembly.GetName().Version?.ToString() ?? "unknown",
            osDescription = RuntimeInformation.OSDescription,
            osVersion = Environment.OSVersion.VersionString,
            clrVersion = Environment.Version.ToString(),
            processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            spotifyVersion = spotify?.DetectedVersion,
            spicetifyVersion = spicetify?.DetectedVersion,
            catalogPins = new
            {
                configSchemaVersion = AppCatalog.CurrentConfigSchemaVersion,
                spotX = new
                {
                    version = AppCatalog.PinnedSpotXVersion,
                    spotifyTargetId = AppCatalog.PinnedSpotXSpotifyVersionId,
                    spotifyTargetVersion = AppCatalog.PinnedSpotXSpotifyVersion,
                    commit = AppCatalog.PinnedSpotXCommit
                },
                spicetifyCli = new
                {
                    version = AppCatalog.PinnedSpicetifyCliVersion,
                    windowsMinTestedSpotify = AppCatalog.SpicetifyWindowsMinTestedSpotify,
                    windowsMaxTestedSpotify = AppCatalog.SpicetifyWindowsMaxTestedSpotify
                },
                marketplace = new
                {
                    version = AppCatalog.PinnedMarketplaceVersion
                },
                themes = new
                {
                    commit = AppCatalog.PinnedThemesCommit
                }
            }
        };
    }

    private string BuildCurrentRunLog(SupportBundleRunContext currentRun)
    {
        var builder = new StringBuilder();
        builder.AppendLine("LibreSpot current run activity log");
        builder.AppendLine($"Captured: {currentRun.CapturedAt:o}");
        builder.AppendLine($"Outcome: {RedactText(currentRun.Outcome)}");
        builder.AppendLine($"Title: {RedactText(currentRun.Title)}");
        builder.AppendLine($"Status: {RedactText(currentRun.Status)}");
        builder.AppendLine($"Step: {RedactText(currentRun.Step)}");
        builder.AppendLine();

        if (currentRun.LogLines.Count == 0)
        {
            builder.AppendLine("No in-memory activity log lines were available.");
            return builder.ToString();
        }

        foreach (var line in currentRun.LogLines.TakeLast(MaxLogLines))
        {
            builder.AppendLine(RedactText(line));
        }

        return builder.ToString();
    }

    private object BuildCurrentRunMetadata(SupportBundleRunContext currentRun) =>
        new
        {
            schemaVersion = 1,
            capturedAt = currentRun.CapturedAt,
            startedAt = currentRun.StartedAt,
            completedAt = currentRun.CompletedAt,
            title = RedactText(currentRun.Title),
            status = RedactText(currentRun.Status),
            step = RedactText(currentRun.Step),
            outcome = RedactText(currentRun.Outcome),
            backendAction = RedactNullable(currentRun.BackendAction),
            backendErrorCode = RedactNullable(currentRun.BackendErrorCode),
            backendErrorMessage = RedactNullable(currentRun.BackendErrorMessage),
            logLineCount = currentRun.LogLines.Count
        };

    private string BuildOperationJournal()
    {
        var builder = new StringBuilder();
        builder.AppendLine("LibreSpot support operation journal");
        builder.AppendLine($"Generated: {DateTimeOffset.Now:o}");
        builder.AppendLine("Network upload: none");
        builder.AppendLine();

        foreach (var file in OperationFiles())
        {
            builder.AppendLine($"## {file.Label}");
            builder.AppendLine($"Source: {RedactText(file.Path)}");
            if (!File.Exists(file.Path))
            {
                builder.AppendLine("Not found.");
                builder.AppendLine();
                continue;
            }

            builder.AppendLine(ReadTailRedacted(file.Path, MaxOperationLines));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private IEnumerable<SupportBundleFile> OperationFiles()
    {
        yield return new SupportBundleFile(Path.Combine(_configDirectory, "operation-journal.jsonl"), "Operation journal JSONL");
        yield return new SupportBundleFile(Path.Combine(_configDirectory, "install.log"), "Backend install log tail");
        yield return new SupportBundleFile(Path.Combine(_configDirectory, "watcher.log"), "Auto-reapply watcher log tail");
        yield return new SupportBundleFile(Path.Combine(_configDirectory, "watcher-state.json"), "Auto-reapply watcher state");
        yield return new SupportBundleFile(Path.Combine(_configDirectory, "spicetify-preservation-latest.json"), "Spicetify preservation evidence");
    }

    private IEnumerable<SupportBundleFile> LogFiles()
    {
        foreach (var file in OperationFiles())
        {
            yield return file;
        }

        foreach (var file in LatestFiles(_rollingLogDirectory, "librespot-*.log", MaxRollingLogFiles))
        {
            yield return new SupportBundleFile(file, "Desktop rolling log");
        }
    }

    private IEnumerable<SupportBundleFile> CrashFiles() =>
        LatestFiles(_crashDirectory, "crash-*.log", MaxCrashFiles)
            .Select(path => new SupportBundleFile(path, "Crash report"));

    private bool TryAddRedactedFileWindow(ZipArchive archive, string folder, SupportBundleFile file, int maxLines)
    {
        if (!File.Exists(file.Path))
        {
            return false;
        }

        var leaf = MakeSafeFileName(System.IO.Path.GetFileName(file.Path));
        var entryName = $"{folder}/{leaf}.tail.txt";
        var header = $"{file.Label}{Environment.NewLine}Source: {RedactText(file.Path)}{Environment.NewLine}{Environment.NewLine}";
        AddTextEntry(archive, entryName, header + ReadTailRedacted(file.Path, maxLines));
        return true;
    }

    private string ReadTailRedacted(string path, int maxLines)
    {
        try
        {
            var lines = File.ReadLines(path, StrictUtf8NoBom)
                .TakeLast(maxLines);
            return RedactText(string.Join(Environment.NewLine, lines));
        }
        catch (DecoderFallbackException)
        {
            return "<omitted: file is not UTF-8 text>";
        }
        catch (IOException ex)
        {
            return RedactText($"<unavailable: {ex.Message}>");
        }
        catch (UnauthorizedAccessException ex)
        {
            return RedactText($"<unavailable: {ex.Message}>");
        }
    }

    private void AddJsonEntry(ZipArchive archive, string entryName, object payload) =>
        AddTextEntry(archive, entryName, JsonSerializer.Serialize(payload, JsonOptions));

    private static void AddTextEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private string? RedactNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? value : RedactText(value);

    private long EstimateHealthBytes(EnvironmentSnapshot snapshot) =>
        MetadataEstimateBytes + snapshot.HealthReport.Components.Sum(component =>
            EstimateTextBytes(component.Name) +
            EstimateTextBytes(component.Status) +
            EstimateTextBytes(component.Path) +
            EstimateTextBytes(component.Evidence));

    private static long EstimateFiles(IEnumerable<SupportBundleFile> files, int maxLines)
    {
        var maxWindowBytes = maxLines * 220L;
        return files
            .Where(file => File.Exists(file.Path))
            .Sum(file =>
            {
                try
                {
                    return Math.Min(new FileInfo(file.Path).Length, maxWindowBytes);
                }
                catch
                {
                    return 0;
                }
            });
    }

    private static int CountExisting(IEnumerable<SupportBundleFile> files) =>
        files.Count(file => File.Exists(file.Path));

    private static long EstimateTextBytes(string? value) =>
        string.IsNullOrEmpty(value) ? 0 : Encoding.UTF8.GetByteCount(value);

    private static IEnumerable<string> LatestFiles(string directory, string pattern, int maxCount)
    {
        if (!Directory.Exists(directory))
        {
            return Array.Empty<string>();
        }

        try
        {
            return Directory.EnumerateFiles(directory, pattern)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(maxCount)
                .Select(file => file.FullName)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string MakeSafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var safe = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "file" : safe;
    }

    private sealed record SupportBundleFile(string Path, string Label);

    private sealed class SupportBundleRedactor
    {
        public static IReadOnlyList<string> RuleDescriptions { get; } = new[]
        {
            "Replaces local user, machine, profile, AppData, LocalAppData, temp, and LibreSpot config paths with placeholders, including JSON-escaped path forms.",
            "Redacts Authorization and GitHub response/request headers.",
            "Redacts GitHub tokens, API keys, passwords, proxy credentials, and command-line secret arguments.",
            "Omits binary or unreadable file payloads from text windows."
        };

        // Bundles can embed attacker-influenced log content (upstream release
        // notes, process output). A match timeout bounds any pathological
        // backtracking; on timeout the whole window is omitted (fail closed)
        // rather than shipped un-redacted.
        private static readonly TimeSpan RuleTimeout = TimeSpan.FromSeconds(2);

        private readonly IReadOnlyList<(Regex Pattern, string Replacement)> _regexRules;
        private readonly IReadOnlyList<(string Value, string Replacement)> _literalRules;

        public SupportBundleRedactor(string configDirectory)
        {
            _literalRules = BuildLiteralRules(configDirectory);
            _regexRules = new[]
            {
                (new Regex(@"(?im)^(?<prefix>\s*(authorization|proxy-authorization)\s*[:=]\s*).+$", RegexOptions.Compiled, RuleTimeout), "${prefix}<redacted>"),
                (new Regex(@"(?im)^(?<prefix>\s*x-(github|oauth|accepted-oauth|ratelimit)-[A-Za-z0-9-]+\s*:\s*).+$", RegexOptions.Compiled, RuleTimeout), "${prefix}<redacted>"),
                (new Regex(@"(?i)\b(ghp|gho|ghu|ghs|ghr|github_pat)_[A-Za-z0-9_]{12,}\b", RegexOptions.Compiled, RuleTimeout), "<redacted-github-token>"),
                (new Regex(@"(?i)\b(?<key>[A-Z0-9_]*(TOKEN|SECRET|PASSWORD|PASS|API_KEY|PAT|PROXY)[A-Z0-9_]*)\s*[:=]\s*(?<value>[^\s;]+)", RegexOptions.Compiled, RuleTimeout), "${key}=<redacted>"),
                (new Regex(@"(?i)(?<prefix>[?&](token|secret|password|pass|api[_-]?key|pat|proxy)=)[^&#\s""]+", RegexOptions.Compiled, RuleTimeout), "${prefix}<redacted>"),
                (new Regex(@"(?i)\b(?<scheme>https?|socks5?)://[^/\s:@]+:[^@\s/]+@", RegexOptions.Compiled, RuleTimeout), "${scheme}://<redacted>@"),
                (new Regex(@"(?i)(--?(token|password|secret|api-key|proxy)\s+)(\S+)", RegexOptions.Compiled, RuleTimeout), "$1<redacted>")
            };
        }

        public string Redact(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            try
            {
                var redacted = text;
                foreach (var (value, replacement) in _literalRules)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        redacted = Regex.Replace(redacted, Regex.Escape(value), replacement, RegexOptions.IgnoreCase, RuleTimeout);
                    }
                }

                foreach (var (pattern, replacement) in _regexRules)
                {
                    redacted = pattern.Replace(redacted, replacement);
                }

                return redacted;
            }
            catch (RegexMatchTimeoutException)
            {
                return "<content omitted: redaction rule timed out>";
            }
        }

        private static IReadOnlyList<(string Value, string Replacement)> BuildLiteralRules(string configDirectory)
        {
            var rules = new List<(string Value, string Replacement)>();
            AddPathRule(rules, configDirectory, "<LIBRESPOT_CONFIG>");
            AddPathRule(rules, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "<USERPROFILE>");
            AddPathRule(rules, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "<APPDATA>");
            AddPathRule(rules, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "<LOCALAPPDATA>");
            AddPathRule(rules, Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), "<TEMP>");
            AddLiteralRule(rules, Environment.UserName, "<USER>");
            AddLiteralRule(rules, Environment.MachineName, "<MACHINE>");
            return rules;
        }

        private static void AddPathRule(List<(string Value, string Replacement)> rules, string? value, string replacement)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var fullPath = Path.GetFullPath(value);
            AddLiteralRule(rules, fullPath, replacement);
            AddLiteralRule(rules, fullPath.Replace(@"\", @"\\"), replacement);
            AddLiteralRule(rules, fullPath.Replace('\\', '/'), replacement);
        }

        private static void AddLiteralRule(List<(string Value, string Replacement)> rules, string? value, string replacement)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length < 3)
            {
                return;
            }

            if (rules.Any(rule => string.Equals(rule.Value, value, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            rules.Add((value, replacement));
        }
    }
}
