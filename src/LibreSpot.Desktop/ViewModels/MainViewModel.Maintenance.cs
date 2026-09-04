using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Properties;
using LibreSpot.Desktop.Services;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using ImageSource = System.Windows.Media.ImageSource;

namespace LibreSpot.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    private IAsyncRelayCommand? _restoreSafeModeCommand;
    private bool _uiAutomationSafeModeRestoreAvailable;
    private bool _uiAutomationMinidumpEnabled;

    private string SafeModeStatePath => Path.Combine(_configurationService.ConfigDirectory, "safe-mode-session.json");

    public bool IsSafeModeRestoreAvailable
    {
        get
        {
            if (_uiAutomationSafeModeRestoreAvailable)
            {
                return true;
            }

            try
            {
                var marker = new FileInfo(SafeModeStatePath);
                if (!marker.Exists || marker.Length is <= 0 or > 4 * 1024 * 1024)
                {
                    return false;
                }

                using var stream = File.Open(SafeModeStatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var document = JsonDocument.Parse(stream);
                var root = document.RootElement;
                var status = root.TryGetProperty("status", out var statusValue) ? statusValue.GetString() : null;
                return root.TryGetProperty("schemaVersion", out var schemaVersion) &&
                    schemaVersion.GetInt32() is 1 or 2 &&
                    status is "ReadyToEnter" or "Active";
            }
            catch
            {
                return false;
            }
        }
    }

    public bool IsSafeModeRestoreUnavailable => !IsSafeModeRestoreAvailable;

    public bool IsMinidumpEnabled
    {
        get => _uiAutomationMinidumpEnabled || _minidumpSettingsService.IsEnabled;
        set
        {
            if (IsMinidumpEnabled == value && !_uiAutomationMinidumpEnabled)
            {
                return;
            }

            try
            {
                _minidumpSettingsService.SetEnabled(value);
                _uiAutomationMinidumpEnabled = false;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MinidumpStatusText));
                AppendLog(L(value ? "MinidumpEnabledLog" : "MinidumpDisabledLog"), "INFO");
                RefreshSupportBundlePreview();
            }
            catch (Exception ex)
            {
                OnPropertyChanged();
                OnPropertyChanged(nameof(MinidumpStatusText));
                var detail = LF("MinidumpUpdateFailedLogFormat", ex.Message);
                AppendLog(detail, "ERROR");
                ShowNotice(L("MinidumpUpdateFailedTitle"), detail, L("MinidumpUpdateFailedDetail"));
            }
        }
    }

    public string MinidumpStatusText => IsMinidumpEnabled
        ? L("MinidumpEnabledStatus")
        : L("MinidumpDisabledStatus");

    public IAsyncRelayCommand RestoreSafeModeCommand =>
        _restoreSafeModeCommand ??= CreateAsyncCommand(
            () => RunMaintenanceAsync(new MaintenanceActionDefinition(
                "RestoreSafeMode",
                L("SafeModeRestoreTitle"),
                L("SafeModeRestoreDescription"),
                L("SafeModeRestoreButton"))),
            () => !IsRunning && IsSafeModeRestoreAvailable);

    private StackHealthComponent? HealthComponent(string id) =>
        HealthReport.Components.FirstOrDefault(component => string.Equals(component.Id, id, StringComparison.OrdinalIgnoreCase));

    private IReadOnlyList<HealthIssueViewModel> BuildHealthIssues(IReadOnlyList<StackHealthComponent> components) =>
        components.Select(component => new HealthIssueViewModel(
                component,
                component.RecommendedActionIds
                    .Select(BuildHealthIssueAction)
                    .OfType<HealthIssueActionViewModel>()
                    .ToArray()))
            .ToArray();

    private HomeActionViewModel BuildHomeAction()
    {
        if (IsSnapshotLoading)
        {
            return new HomeActionViewModel(
                HomeActionKind.Checking,
                "Checking",
                L("Vm_SimpleHomeCheckingTitle"),
                L("Vm_SimpleHomeCheckingBody"),
                L("Vm_HomeCheckingAction"),
                null,
                false,
                L("Vm_HomeCheckingAction"),
                L("Vm_SimpleHomeCheckingBody"),
                HealthSeverity.Info,
                false);
        }

        if (HasSnapshotLoadError)
        {
            return new HomeActionViewModel(
                HomeActionKind.Retry,
                "Retry",
                L("Vm_SimpleHomeUnavailableTitle"),
                L("Vm_SimpleHomeUnavailableBody"),
                L("Vm_ShellRetryShort"),
                RefreshSnapshotCommand,
                RefreshSnapshotCommand.CanExecute(null),
                L("Vm_ShellRetryShort"),
                L("ButtonRefreshEnvironmentHint"),
                HealthSeverity.Warning,
                false);
        }

        var actionableIssues = HealthReport.CriticalIssues
            .Concat(HealthReport.WarningIssues)
            .SelectMany(component => component.RecommendedActionIds
                .Select(BuildHealthIssueAction)
                .OfType<HealthIssueActionViewModel>()
                .Select(action => (Component: component, Action: action)))
            .ToArray();
        var safeAction = actionableIssues.FirstOrDefault(candidate => !candidate.Action.IsDestructive);
        if (safeAction.Action is not null)
        {
            return new HomeActionViewModel(
                HomeActionKind.HealthRepair,
                safeAction.Action.Action,
                safeAction.Component.Status,
                safeAction.Component.Evidence,
                safeAction.Action.ButtonText,
                safeAction.Action.Command,
                safeAction.Action.Command.CanExecute(null),
                safeAction.Action.ButtonText,
                safeAction.Action.Description,
                safeAction.Component.Severity,
                false);
        }

        var spotX = HealthComponent("spotx");
        var managedStackIsHealthy = Snapshot.SpotifyInstalled &&
            Snapshot.SpicetifyInstalled &&
            spotX?.Severity == HealthSeverity.Ready &&
            actionableIssues.Length == 0;
        if (managedStackIsHealthy)
        {
            return new HomeActionViewModel(
                HomeActionKind.OpenSpotify,
                "OpenSpotify",
                L("Vm_HomeManagedReadyTitle"),
                L("Vm_HomeManagedReadyBody"),
                L("Vm_HomeOpenSpotifyAction"),
                OpenSpotifyCommand,
                OpenSpotifyCommand.CanExecute(null),
                L("Vm_HomeOpenSpotifyAction"),
                L("Vm_HomeOpenSpotifyHelp"),
                HealthSeverity.Ready,
                false);
        }

        var destructiveAction = actionableIssues.FirstOrDefault(candidate => candidate.Action.IsDestructive);
        if (destructiveAction.Action is not null)
        {
            return new HomeActionViewModel(
                HomeActionKind.Maintenance,
                "Maintenance",
                L("Vm_SimpleHomeAttentionTitle"),
                L("Vm_HomeDestructiveOnlyBody"),
                L("Vm_HomeOpenMaintenanceAction"),
                ShowMaintenanceWorkspaceCommand,
                ShowMaintenanceWorkspaceCommand.CanExecute(null),
                L("Vm_HomeOpenMaintenanceAction"),
                L("Vm_HomeOpenMaintenanceHelp"),
                destructiveAction.Component.Severity,
                false);
        }

        return new HomeActionViewModel(
            HomeActionKind.RecommendedSetup,
            "Install",
            L("Vm_SimpleHomeReadyTitle"),
            Snapshot.SpotifyInstalled
                ? L("Vm_SimpleHomeReadyBody")
                : L("Vm_SimpleHomeInstallBody"),
            L("ButtonStartRecommendedSetup"),
            ApplyRecommendedCommand,
            ApplyRecommendedCommand.CanExecute(null),
            L("ButtonStartRecommendedSetup"),
            L("Ui_RunsLibreSpotSMostReliableInstallPathWith"),
            HealthSeverity.Ready,
            true);
    }

    private HomeActionViewModel BuildMaintenanceRecommendation()
    {
        var homeAction = BuildHomeAction();
        if (homeAction.Kind == HomeActionKind.Checking)
        {
            return homeAction;
        }

        if (homeAction.Kind == HomeActionKind.Retry)
        {
            return new HomeActionViewModel(
                homeAction.Kind,
                homeAction.ActionId,
                homeAction.Title,
                L("Vm_MaintenanceUnavailableBody"),
                homeAction.PrimaryLabel,
                homeAction.Command,
                homeAction.IsEnabled,
                homeAction.AutomationName,
                homeAction.HelpText,
                homeAction.Tone,
                false);
        }

        var highestPriorityIssue = HealthReport.CriticalIssues
            .Concat(HealthReport.WarningIssues)
            .FirstOrDefault();
        if (highestPriorityIssue is not null)
        {
            if (homeAction.Kind == HomeActionKind.HealthRepair)
            {
                return new HomeActionViewModel(
                    HomeActionKind.HealthRepair,
                    homeAction.ActionId,
                    highestPriorityIssue.Status,
                    highestPriorityIssue.Evidence,
                    homeAction.PrimaryLabel,
                    homeAction.Command,
                    homeAction.IsEnabled,
                    homeAction.AutomationName,
                    homeAction.HelpText,
                    highestPriorityIssue.Severity,
                    false);
            }

            return new HomeActionViewModel(
                HomeActionKind.ReviewNeeded,
                "Review",
                highestPriorityIssue.Status,
                LF("Vm_MaintenanceReviewBodyFormat", highestPriorityIssue.Evidence),
                L("StatusNeedsReview"),
                null,
                false,
                L("StatusNeedsReview"),
                L("ResetDescription"),
                highestPriorityIssue.Severity,
                false);
        }

        if (homeAction.Kind == HomeActionKind.OpenSpotify)
        {
            return new HomeActionViewModel(
                HomeActionKind.NoActionNeeded,
                "NoActionNeeded",
                L("Vm_MaintenanceNoActionTitle"),
                L("Vm_MaintenanceReadinessAllReady"),
                L("Vm_MaintenanceNoActionTitle"),
                null,
                false,
                L("Vm_MaintenanceNoActionTitle"),
                L("Vm_MaintenanceReadinessAllReady"),
                HealthSeverity.Ready,
                false);
        }

        return homeAction;
    }

    private HealthIssueActionViewModel? BuildHealthIssueAction(string action)
    {
        var maintenanceCard = _maintenanceActions.Find(action);
        if (maintenanceCard is not null && maintenanceCard.IsRelevant)
        {
            return new HealthIssueActionViewModel(
                maintenanceCard.Action,
                maintenanceCard.ButtonText,
                maintenanceCard.Description,
                maintenanceCard.IsDestructive,
                maintenanceCard.Command);
        }

        return action switch
        {
            "Install" => new HealthIssueActionViewModel(
                action,
                Strings.ButtonRunRecommendedSetup,
                Strings.RecommendedModeHint,
                false,
                ApplyRecommendedCommand),
            "EnableAutoReapply" => new HealthIssueActionViewModel(
                action,
                Strings.ButtonEnableWatcher,
                Strings.ButtonEnableWatcherHint,
                false,
                EnableAutoReapplyCommand),
            "OpenLogs" or "WatchAutoReapply" => new HealthIssueActionViewModel(
                action,
                Strings.ButtonOpenLibreSpotFolder,
                Strings.ButtonOpenLibreSpotFolderActivityHint,
                false,
                OpenLibreSpotFolderCommand),
            "ClearCache" => new HealthIssueActionViewModel(
                action,
                L("Vm_ShellClearCacheTitle"),
                L("Vm_ClearCacheActionDescription"),
                false,
                CreateAsyncCommand(
                    () => RunMaintenanceAsync(new MaintenanceActionDefinition(
                        "ClearCache",
                        L("Vm_ClearAssetCacheTitle"),
                        L("Vm_ClearCacheActionDescription"),
                        L("Vm_ShellClearCacheTitle"))),
                    () => !IsRunning)),
            _ => null
        };
    }

    private StatusDashboardItemViewModel BuildDashboardItem(
        string label,
        StackHealthComponent? component,
        Func<StackHealthComponent, string> valueFactory)
    {
        if (component is null)
        {
            return new StatusDashboardItemViewModel(
                label,
                Strings.DashboardUnknownValue,
                Strings.DashboardSnapshotMissingDetail,
                HealthSeverity.Info);
        }

        var detail = component.HasLastChanged
            ? LF("Vm_HealthEvidenceLastChangedFormat", component.Evidence, component.LastChangedDisplay)
            : component.Evidence;

        return new StatusDashboardItemViewModel(
            label,
            valueFactory(component),
            detail,
            component.Severity);
    }

    private CompatibilityVerdictItemViewModel BuildCompatibilityVerdictItem(
        CompatibilityVerdictItem item)
    {
        var label = item.Id switch
        {
            "spotify" => Strings.DashboardSpotifyVersionLabel,
            "spotx" => Strings.DashboardSpotXStateLabel,
            "spicetify-cli" => Strings.DashboardSpicetifyVersionLabel,
            "marketplace" => Strings.MarketplaceLabel,
            _ => L("CompatibilityDetectedUnknown")
        };
        var verdict = LocalizeCompatibilityVerdict(item.Verdict);
        return new CompatibilityVerdictItemViewModel(
            $"CompatibilityVerdict_{item.Id}",
            label,
            LocalizeCompatibilityDetection(item),
            item.PinnedValue,
            verdict,
            LocalizeCompatibilityNextStep(item.Verdict),
            CompatibilityTone(item.Verdict));
    }

    private string LocalizeCompatibilityDetection(CompatibilityVerdictItem item) =>
        item.DetectionCode switch
        {
            CompatibilityDetectionCode.Version when !string.IsNullOrWhiteSpace(item.DetectedValue) =>
                item.DetectedValue!,
            CompatibilityDetectionCode.Missing => L("CompatibilityDetectedMissing"),
            CompatibilityDetectionCode.NotChecked => L("CompatibilityDetectedNotChecked"),
            CompatibilityDetectionCode.Verified => L("CompatibilityDetectedVerified"),
            CompatibilityDetectionCode.Unverified => L("CompatibilityDetectedUnverified"),
            CompatibilityDetectionCode.Files => L("CompatibilityDetectedFiles"),
            CompatibilityDetectionCode.Unavailable => L("CompatibilityDetectedUnavailable"),
            _ => L("CompatibilityDetectedUnknown")
        };

    private string LocalizeCompatibilityVerdict(string verdict) =>
        verdict switch
        {
            CompatibilityVerdictState.Supported => L("CompatibilityVerdictSupported"),
            CompatibilityVerdictState.Degraded => L("CompatibilityVerdictDegraded"),
            CompatibilityVerdictState.Unsupported => L("CompatibilityVerdictUnsupported"),
            _ => L("CompatibilityVerdictUnknown")
        };

    private string LocalizeCompatibilityNextStep(string verdict) =>
        verdict switch
        {
            CompatibilityVerdictState.Supported => L("CompatibilityNextStepSupported"),
            CompatibilityVerdictState.Degraded => L("CompatibilityNextStepDegraded"),
            CompatibilityVerdictState.Unsupported => L("CompatibilityNextStepUnsupported"),
            _ => L("CompatibilityNextStepUnknown")
        };

    private static string CompatibilityTone(string verdict) =>
        verdict switch
        {
            CompatibilityVerdictState.Supported => HealthSeverity.Ready,
            CompatibilityVerdictState.Degraded => HealthSeverity.Warning,
            CompatibilityVerdictState.Unsupported => HealthSeverity.Critical,
            _ => HealthSeverity.Info
        };

    private static ShellDependencyRowViewModel BuildDependencyRow(
        string label,
        StackHealthComponent? component,
        string recommended)
    {
        if (component is null)
        {
            return new ShellDependencyRowViewModel(
                label,
                Strings.DashboardUnknownValue,
                recommended,
                HealthSeverity.Info);
        }

        return new ShellDependencyRowViewModel(
            label,
            FirstNonEmpty(component.DetectedVersion, component.Status),
            recommended,
            component.Severity);
    }

    private ProvenanceItemViewModel BuildProvenanceItem(UpstreamDependencyPin pin)
    {
        var state = Snapshot.UpstreamDriftReport.Dependencies.FirstOrDefault(dependency =>
            string.Equals(dependency.Id, pin.Id, StringComparison.OrdinalIgnoreCase));
        var freshness = state?.FreshnessStatus ?? ProvenanceFreshness.Indeterminate;
        var sourceUrl = FirstNonEmpty(state?.SourceUrl, pin.SourceUrl);
        var releaseNotesUrl = FirstNonEmpty(state?.ReleaseNotesUrl, pin.ReleaseNotesUrl);
        var verifiedAt = state?.LastVerifiedAtUtc ?? pin.LastVerifiedAtUtc;
        var verifiedDetail = verifiedAt.HasValue
            ? LF(
                "Vm_ShellProvenanceVerifiedFormat",
                verifiedAt.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))
            : L("Vm_ShellProvenanceVerifiedUnknown");

        return new ProvenanceItemViewModel(
            pin.Name,
            LF("Vm_ShellProvenancePinnedFormat", state?.PinnedValue ?? pin.PinnedValue),
            sourceUrl,
            releaseNotesUrl,
            verifiedDetail,
            LocalizeProvenanceFreshness(freshness),
            ProvenanceTone(freshness),
            L("Vm_ShellProvenanceOpenSource"),
            L("Vm_ShellProvenanceOpenReleaseNotes"),
            OpenExternalUri);
    }

    private string LocalizeProvenanceFreshness(string freshness) => freshness switch
    {
        ProvenanceFreshness.Current => L("Vm_ShellProvenanceCurrent"),
        ProvenanceFreshness.Stale => L("Vm_ShellProvenanceStale"),
        ProvenanceFreshness.Missing => L("Vm_ShellProvenanceMissing"),
        ProvenanceFreshness.Ahead => L("Vm_ShellProvenanceAhead"),
        _ => L("Vm_ShellProvenanceIndeterminate")
    };

    private static string ProvenanceTone(string freshness) => freshness switch
    {
        ProvenanceFreshness.Current => HealthSeverity.Ready,
        ProvenanceFreshness.Stale or ProvenanceFreshness.Missing or ProvenanceFreshness.Ahead => HealthSeverity.Warning,
        _ => HealthSeverity.Info
    };

    private StatusDashboardItemViewModel BuildLastPatchDashboardItem()
    {
        var postUpdate = HealthComponent("post-spotify-update");
        var spotx = HealthComponent("spotx");
        var timestampSource = postUpdate?.HasLastChanged == true ? postUpdate : spotx;
        var evidenceSource = postUpdate ?? spotx;

        return new StatusDashboardItemViewModel(
            Strings.DashboardLastPatchLabel,
            timestampSource?.HasLastChanged == true ? timestampSource.LastChangedDisplay : Strings.DashboardNoPatchRecord,
            evidenceSource?.Evidence ?? Strings.DashboardNoPatchRecordDetail,
            evidenceSource?.Severity ?? HealthSeverity.Info);
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? Strings.DashboardUnknownValue;

    private bool HasRecommendedAction(string action) =>
        HealthReport.Components.Any(component => component.RecommendedActionIds.Contains(action, StringComparer.Ordinal));

    private void RefreshMaintenanceActionRelevance()
    {
        _maintenanceActions.RefreshRelevance(IsMaintenanceActionRelevant);
        OnPropertyChanged(nameof(IsSafeModeRestoreAvailable));
        OnPropertyChanged(nameof(IsSafeModeRestoreUnavailable));
        _restoreSafeModeCommand?.NotifyCanExecuteChanged();
    }

    private void RaiseMaintenanceActionCanExecuteChanged() => _maintenanceActions.RaiseCanExecuteChanged();

    private static IAsyncRelayCommand CreateSafeAsyncCommand(
        Func<Task> executeAsync,
        Action<Exception> onException,
        Func<bool>? canExecute = null) =>
        canExecute is null
            ? new AsyncRelayCommand(() => ExecuteSafeAsync(executeAsync, onException))
            : new AsyncRelayCommand(() => ExecuteSafeAsync(executeAsync, onException), canExecute);

    private IAsyncRelayCommand CreateAsyncCommand(Func<Task> executeAsync, Func<bool>? canExecute = null) =>
        CreateSafeAsyncCommand(executeAsync, HandleAsyncCommandException, canExecute);

    private static async Task ExecuteSafeAsync(Func<Task> executeAsync, Action<Exception> onException)
    {
        try
        {
            await executeAsync();
        }
        catch (Exception ex)
        {
            onException(ex);
        }
    }

    private void HandleAsyncCommandException(Exception ex)
    {
        if (ex is OperationCanceledException)
        {
            return;
        }

        AppendLog(LF("Vm_LogDesktopCommandFailed", ex.Message), "ERROR");
        ShowNotice(
            L("Vm_ActionCouldNotFinish"),
            ex.Message,
            L("Vm_ActionCouldNotFinishDetail"));
    }

    private bool IsMaintenanceActionRelevant(string action)
    {
        if (IsSafeModeRestoreAvailable)
        {
            return false;
        }

        var marketplace = HealthComponent("marketplace");
        var backups = HealthComponent("backups");
        var spicetifyConfig = HealthComponent("spicetify-config");
        var savedProfile = HealthComponent("saved-profile");
        var logs = HealthComponent("logs");

        return action switch
        {
            "CheckUpdates" => true,
            "Reapply" => Snapshot.SpotifyInstalled && (Snapshot.SpicetifyInstalled || HealthReport.HasCriticalIssues || HealthReport.HasWarningIssues),
            "RepairMarketplace" => Snapshot.SpicetifyInstalled && marketplace?.Severity is HealthSeverity.Warning or HealthSeverity.Critical,
            "OpenMarketplace" => Snapshot.MarketplaceFilesPresent && Snapshot.MarketplaceRegistered,
            "SafeMode" => Snapshot.SpicetifyInstalled &&
                !IsSafeModeRestoreAvailable &&
                HealthComponent("active-theme")?.Status != L("HealthStatusMarketplaceOrStock"),
            "CreateBackup" => Snapshot.SpicetifyInstalled && spicetifyConfig?.Severity == HealthSeverity.Ready,
            "RestoreBackup" => Snapshot.SpicetifyInstalled && backups?.Severity == HealthSeverity.Ready,
            "RestoreVanilla" => Snapshot.SpicetifyInstalled,
            "UninstallSpicetify" => Snapshot.SpicetifyInstalled,
            "FullReset" => Snapshot.SpotifyInstalled || Snapshot.SpicetifyInstalled || HealthReport.HasCriticalIssues,
            "RemoveSelfData" => savedProfile?.Severity == HealthSeverity.Ready || logs?.Severity == HealthSeverity.Ready || backups?.Severity == HealthSeverity.Ready || Snapshot.ConfigFolderExists,
            _ => HasRecommendedAction(action)
        };
    }

    private void InitializeSupportBundleItems()
    {
        SupportBundleItems.Clear();
        SupportBundleItems.Add(new SupportBundleCategoryViewModel(
            "health",
            L("Vm_SupportBundleCategoryHealthTitle"),
            true,
            true,
            L("Vm_SupportBundleCategoryHealthDetail"),
            OnSupportBundleSelectionChanged));
        SupportBundleItems.Add(new SupportBundleCategoryViewModel(
            "operation",
            L("Vm_SupportBundleCategoryOperationTitle"),
            false,
            true,
            L("Vm_SupportBundleCategoryOperationDetail"),
            OnSupportBundleSelectionChanged));
        SupportBundleItems.Add(new SupportBundleCategoryViewModel(
            "logs",
            L("Vm_SupportBundleCategoryLogsTitle"),
            false,
            true,
            L("Vm_SupportBundleCategoryLogsDetail"),
            OnSupportBundleSelectionChanged));
        SupportBundleItems.Add(new SupportBundleCategoryViewModel(
            "crashes",
            L("Vm_SupportBundleCategoryCrashesTitle"),
            false,
            true,
            L("Vm_SupportBundleCategoryCrashesDetail"),
            OnSupportBundleSelectionChanged));
    }

    private void OnSupportBundleSelectionChanged() => RefreshSupportBundlePreview();

    private SupportBundleOptions BuildSupportBundleOptions(SupportBundleRunContext? currentRun = null) =>
        new(
            IncludeOperationJournal: SupportBundleItems.FirstOrDefault(item => item.Id == "operation")?.IsSelected ?? true,
            IncludeLogs: SupportBundleItems.FirstOrDefault(item => item.Id == "logs")?.IsSelected ?? true,
            IncludeCrashReports: SupportBundleItems.FirstOrDefault(item => item.Id == "crashes")?.IsSelected ?? true,
            CurrentRun: currentRun,
            IncludeMinidump: IsMinidumpEnabled);

    private void RefreshSupportBundlePreview()
    {
        _supportBundlePreview = _supportBundleService.CreatePreview(Snapshot, BuildSupportBundleOptions());

        foreach (var entry in _supportBundlePreview.Entries)
        {
            SupportBundleItems.FirstOrDefault(item => item.Id == entry.Id)?.Refresh(entry);
        }

        SupportBundleRedactionRules.Clear();
        foreach (var rule in _supportBundlePreview.RedactionRules)
        {
            SupportBundleRedactionRules.Add(rule);
        }

        RaiseSupportBundlePreviewChanged();
        ExportSupportBundleCommand.NotifyCanExecuteChanged();
    }

    private void RaiseSupportBundlePreviewChanged()
    {
        OnPropertyChanged(nameof(SupportBundlePreviewTitle));
        OnPropertyChanged(nameof(SupportBundlePreviewDetail));
        OnPropertyChanged(nameof(SupportBundleRedactionSummary));
    }

    private async Task ExportSupportBundleAsync()
    {
        if (IsRunning)
        {
            return;
        }

        RefreshSupportBundlePreview();
        var defaultPath = _supportBundleService.CreateDefaultBundlePath();
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = Strings.ExportBundleTitle,
            Filter = L("Vm_ZipArchiveDialogFilter"),
            DefaultExt = ".zip",
            AddExtension = true,
            OverwritePrompt = true,
            InitialDirectory = Path.GetDirectoryName(defaultPath),
            FileName = Path.GetFileName(defaultPath)
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var result = await _supportBundleService.ExportAsync(
                dialog.FileName,
                Snapshot,
                BuildSupportBundleOptions());
            SupportBundleLastExportText = LF("Vm_SupportBundleLastExportFormat", result.Path, FormatBytes(result.BytesWritten), result.EntryCount);
            AppendLog(LF("Vm_SupportBundleExportedLogFormat", result.Path), "SUCCESS");
        }
        catch (Exception ex)
        {
            SupportBundleLastExportText = LF("Vm_SupportBundleExportFailedFormat", ex.Message);
            AppendLog(LF("Vm_SupportBundleExportFailedLogFormat", ex.Message), "ERROR");
        }
    }

    private async Task ExportFailureBundleAsync()
    {
        if (!CanExportFailureBundle)
        {
            return;
        }

        var currentRun = BuildCurrentRunContext();
        var destination = _supportBundleService.CreateDefaultFailureBundlePath();
        try
        {
            var result = await _supportBundleService.ExportAsync(
                destination,
                Snapshot,
                BuildSupportBundleOptions(currentRun));
            SupportBundleLastExportText = LF("Vm_SupportBundleLastFailureExportFormat", result.Path, FormatBytes(result.BytesWritten), result.EntryCount);
            AppendLog(LF("Vm_FailureBundleExportedLogFormat", result.Path), "SUCCESS");
        }
        catch (Exception ex)
        {
            SupportBundleLastExportText = LF("Vm_FailureBundleExportFailedFormat", ex.Message);
            AppendLog(LF("Vm_FailureBundleExportFailedLogFormat", ex.Message), "ERROR");
        }
    }

    private SupportBundleRunContext BuildCurrentRunContext()
    {
        var outcome = IsActivityCanceled
            ? "Canceled"
            : IsActivityError
                ? "Error"
                : ProgressValue >= 100
                    ? "Success"
                    : "Unknown";

        return new SupportBundleRunContext(
            ActivityTitle,
            ActivityStatus,
            ActivityStep,
            outcome,
            _lastBackendAction,
            _lastBackendRunResult?.ErrorCode,
            _lastBackendRunResult?.ErrorMessage,
            _lastRunStartedAt,
            _lastRunCompletedAt,
            DateTimeOffset.Now,
            LogEntries.Select(entry => entry.CopyLine).ToArray(),
            ActivityOperationId);
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = Math.Max(0, bytes);
        var unit = 0;
        var display = (double)value;
        while (display >= 1024 && unit < units.Length - 1)
        {
            display /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{value} {units[unit]}"
            : $"{display:0.#} {units[unit]}";
    }

    private void RaiseAutoReapplyStateChanged()
    {
        OnPropertyChanged(nameof(AutoReapplyStatusTitle));
        OnPropertyChanged(nameof(AutoReapplyStatusDetail));
        OnPropertyChanged(nameof(AutoReapplyTaskLine));
        OnPropertyChanged(nameof(AutoReapplyLogLine));
        EnableAutoReapplyCommand.NotifyCanExecuteChanged();
        DisableAutoReapplyCommand.NotifyCanExecuteChanged();
    }

    private async Task RunMaintenanceAsync(MaintenanceActionDefinition definition)
    {
        if (definition.Action is not ("CheckUpdates" or "EnableAutoReapply" or "DisableAutoReapply"))
        {
            if (!await EnsureRiskAcknowledgedAsync())
            {
                return;
            }
        }

        if (definition.Action is "SafeMode" or "RestoreSafeMode")
        {
            await StartBackendRunAsync(definition.Action, null, definition.Title, definition.Description, 2);
            return;
        }

        var body = definition.Action == "RemoveSelfData"
            ? $"{definition.Description}{Environment.NewLine}{Environment.NewLine}{L("Vm_MaintenanceRemoveSelfDataBodySuffix")}"
            : definition.IsDestructive
                ? $"{definition.Description}{Environment.NewLine}{Environment.NewLine}{L("Vm_MaintenanceDestructiveBodySuffix")}"
                : $"{definition.Description}{Environment.NewLine}{Environment.NewLine}{L("Vm_MaintenanceStandardBodySuffix")}";
        var (summaryTitle, summaryBody) = BuildMaintenancePromptSummary(definition);

        ShowPrompt(
            definition.Title,
            body,
            definition.ButtonText,
            definition.IsDestructive ? L("Vm_KeepCurrentSetup") : Strings.ButtonCancel,
            definition.IsDestructive,
            () => StartBackendRunAsync(definition.Action, null, definition.Title, definition.Description, 2),
            summaryTitle,
            summaryBody);
    }

    private void PresentAutoReapplyPrompt(bool enable)
    {
        if (IsRunning)
        {
            return;
        }

        var action = enable ? "EnableAutoReapply" : "DisableAutoReapply";
        var title = enable ? L("Vm_AutoReapplyEnablePromptTitle") : L("Vm_AutoReapplyDisablePromptTitle");
        var status = enable ? L("Vm_AutoReapplyEnableActivityStatus") : L("Vm_AutoReapplyDisableActivityStatus");
        var body = enable
            ? L("Vm_AutoReapplyEnablePromptBody")
            : L("Vm_AutoReapplyDisablePromptBody");
        var summaryBody = enable
            ? L("Vm_AutoReapplyEnablePromptSummary")
            : L("Vm_AutoReapplyDisablePromptSummary");

        ShowPrompt(
            title,
            body,
            enable ? Strings.ButtonEnableWatcher : Strings.ButtonDisableWatcher,
            Strings.ButtonCancel,
            false,
            () => StartBackendRunAsync(action, null, title, status, 2),
            L("Vm_PromptWhatThisDoes"),
            summaryBody);
    }
}
