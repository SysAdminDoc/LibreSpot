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

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ConfigurationService _configurationService;
    private readonly BackendScriptService _backendScriptService;
    private readonly Func<string, Task<EnvironmentSnapshot>> _snapshotLoader;
    private readonly SupportBundleService _supportBundleService;
    private readonly MinidumpSettingsService _minidumpSettingsService;
    private readonly OperationJournalUndoService _operationJournalUndoService;
    private readonly LocalProfileService _profileService;
    private readonly CustomPatchService _customPatchService;
    private readonly LocalizationService _localizationService;
    private readonly ISpotifyProcessService _spotifyProcessService;
    private readonly Func<string, CancellationToken, Task<ReleaseNotice>>? _releaseNoticeProbe;
    private readonly CancellationTokenSource _releaseNoticeCts = new();
    private ReleaseNotice? _libreSpotUpdateNotice;
    private Task? _libreSpotUpdateCheck;
    private readonly ActivityRunStateViewModel _activityState = new();
    private ActivityOutcome _activityOutcome = ActivityOutcome.None;
    private readonly CustomOptionEditorStateViewModel _customOptions;
    private readonly EnvironmentSnapshotStateViewModel _environmentState = new();
    private readonly PromptStateViewModel _promptState = new();
    private readonly SettingsSearchStateViewModel _settingsSearch = new();
    private readonly Dispatcher _dispatcher;
    private readonly bool _isAdministratorSession;
    private readonly InstallConfiguration _recommendedBaseline;
    private readonly MaintenanceActionsStateViewModel _maintenanceActions;
    private readonly Stopwatch _runStopwatch = new();
    private readonly DispatcherTimer _runElapsedTimer;
    private readonly DispatcherTimer _snapshotFreshnessTimer;
    private CancellationTokenSource? _runCts;
    private string? _lastBackendAction;
    private BackendRunResult? _lastBackendRunResult;
    private DateTimeOffset? _lastRunStartedAt;
    private DateTimeOffset? _lastRunCompletedAt;
    private int _shellLogFilterIndex;
    private int _snapshotRequestVersion;
    private bool _isSnapshotLoading = true;
    private bool _snapshotLoadFailed;
    private bool _isMaintenanceDiagnosticsExpanded;
    private bool _isMaintenanceDangerExpanded;

    private int _selectedWorkspaceIndex;
    private string _globalSearchText = string.Empty;
    private bool _isApplyingSelectionDependencyRules;
    private ConfigurationLoadState _configurationLoadState = ConfigurationLoadState.Loaded;
    private string? _recoveredConfigurationPath;
    private string? _configurationRecoveryReason;
    private LocalProfileCardViewModel? _selectedLocalProfile;
    private string _profileNameText = ViewModelText.Get("Vm_ProfileDefaultName");
    private string _profileDescriptionText = ViewModelText.Get("Vm_ProfileDefaultDescription");
    private string _profileOperationStatus = ViewModelText.Get("Vm_ProfileOperationInitial");
    private LocalProfileShareCard? _selectedProfileShareCard;
    private ImageSource? _selectedProfileQrImage;
    private string _selectedProfileShareStatus = ViewModelText.Get("Vm_ProfileShareInitial");
    private string _selectedProfileComparisonText = ViewModelText.Get("Vm_ProfileComparisonInitial");
    private Task _selectedProfileShareRefreshTask = Task.CompletedTask;
    private bool _customPatchesEnabled;
    private string _customPatchesJson = string.Empty;
    private string _customPatchesImportUrl = string.Empty;
    private string _customPatchesSourceUrl = string.Empty;
    private DateTimeOffset? _customPatchesFetchedAtUtc;
    private int _customPatchesSourceByteCount;
    private string _customPatchesSourceSha256 = string.Empty;
    private bool _preserveCustomPatchProvenance;
    private CustomPatchValidationResult _customPatchValidation;
    private LocalizationOption _selectedLocalizationOption = LocalizationService.SupportedCultures[0];
    private bool _applyingCultureFromConfig;
    private SupportBundlePreview _supportBundlePreview = new(
        Array.Empty<SupportBundlePreviewEntry>(),
        0,
        Array.Empty<string>());
    private string _supportBundleLastExportText = Strings.NoBundleExported;

    public MainViewModel(
        ConfigurationService configurationService,
        BackendScriptService backendScriptService,
        EnvironmentSnapshotService snapshotService,
        SupportBundleService? supportBundleService = null,
        OperationJournalUndoService? operationJournalUndoService = null,
        LocalProfileService? profileService = null,
        CustomPatchService? customPatchService = null,
        LocalizationService? localizationService = null,
        ISpotifyProcessService? spotifyProcessService = null,
        Func<string, Task<EnvironmentSnapshot>>? snapshotLoader = null,
        Func<string, CancellationToken, Task<ReleaseNotice>>? releaseNoticeProbe = null,
        MinidumpSettingsService? minidumpSettingsService = null)
    {
        _configurationService = configurationService;
        _backendScriptService = backendScriptService;
        _snapshotLoader = snapshotLoader ?? snapshotService.GetSnapshotAsync;
        _supportBundleService = supportBundleService ?? new SupportBundleService(configurationService.ConfigDirectory);
        _minidumpSettingsService = minidumpSettingsService ?? new MinidumpSettingsService(configurationService.ConfigDirectory);
        _operationJournalUndoService = operationJournalUndoService ?? new OperationJournalUndoService();
        _profileService = profileService ?? new LocalProfileService(configurationService);
        _customPatchService = customPatchService ?? new CustomPatchService();
        _localizationService = localizationService ?? LocalizationService.Current;
        _spotifyProcessService = spotifyProcessService ?? new SpotifyProcessService();
        _releaseNoticeProbe = releaseNoticeProbe;
        _customPatchValidation = _customPatchService.Validate(string.Empty, enabled: false);
        _selectedLocalizationOption = LocalizationService.SupportedCultures.First(option =>
            string.Equals(option.CultureName, _localizationService.CultureName, StringComparison.OrdinalIgnoreCase));
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _isAdministratorSession = IsAdministrator();
        _recommendedBaseline = AppCatalog.CreateRecommendedConfiguration();
        _customOptions = new CustomOptionEditorStateViewModel(_recommendedBaseline);
        _runElapsedTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _runElapsedTimer.Tick += (_, _) => OnPropertyChanged(nameof(RunElapsedText));
        _snapshotFreshnessTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _snapshotFreshnessTimer.Tick += (_, _) => RaiseSnapshotFreshnessChanged();
        _snapshotFreshnessTimer.Start();
        _activityState.PropertyChanged += OnActivityStatePropertyChanged;
        _customOptions.PropertyChanged += OnCustomOptionEditorPropertyChanged;
        _environmentState.PropertyChanged += OnEnvironmentStatePropertyChanged;
        _promptState.PropertyChanged += OnPromptStatePropertyChanged;
        _settingsSearch.PropertyChanged += OnSettingsSearchStatePropertyChanged;

        RecommendedHighlights = new ObservableCollection<string>(AppCatalog.RecommendedHighlights);
        SelectionInsights = new ObservableCollection<SelectionInsightViewModel>();
        SelectedExtensionLabels = new ObservableCollection<string>();
        SupportBundleItems = new ObservableCollection<SupportBundleCategoryViewModel>();
        SupportBundleRedactionRules = new ObservableCollection<string>();
        ChangelogHighlights = new ObservableCollection<string>(ChangelogPreviewService.LoadUnreleasedHighlights());
        LocalProfiles = new ObservableCollection<LocalProfileCardViewModel>();
        GlobalSearchResults = new ObservableCollection<GlobalSearchResultViewModel>();
        CustomPatchFindings = new ObservableCollection<string>();
        LocalizationOptions = new ObservableCollection<LocalizationOption>(LocalizationService.SupportedCultures);
        _localizationService.CultureChanged += OnLocalizationCultureChanged;

        _maintenanceActions = new MaintenanceActionsStateViewModel(
            AppCatalog.MaintenanceActions,
            RunMaintenanceAsync,
            () => !IsRunning,
            HandleAsyncCommandException);

        ApplyRecommendedCommand = CreateAsyncCommand(ApplyRecommendedAsync, () => !IsRunning && IsEnvironmentReadyForActions);
        ApplyCustomCommand = CreateAsyncCommand(ApplyCustomAsync, () => !IsRunning && IsEnvironmentReadyForActions);
        OpenSpotifyCommand = CreateAsyncCommand(OpenSpotifyAsync, () => !IsRunning && IsEnvironmentReadyForActions && Snapshot.SpotifyInstalled);
        CancelRunCommand = new RelayCommand(CancelRunningBackend, () => IsRunning && !IsCancelRequested);
        DismissActivityCommand = new RelayCommand(DismissActivity, () => IsActivityVisible && !IsRunning);
        CopyOperationIdCommand = new RelayCommand(CopyOperationId, () => HasActivityOperationId);
        CopyLogCommand = new RelayCommand(CopyLog, () => LogEntries.Count > 0);
        ClearLogCommand = new RelayCommand(ClearLog, () => LogEntries.Count > 0);
        PreviewSelectedUndoCommand = new RelayCommand(PreviewSelectedUndo, () => !IsRunning && HasExecutableUndoActionItems);
        ExecuteSelectedUndoCommand = new RelayCommand(PresentSelectedUndoConfirmation, () => !IsRunning && HasExecutableUndoActionItems);
        CycleShellLogFilterCommand = new RelayCommand(CycleShellLogFilter);
        OpenLibreSpotFolderCommand = new RelayCommand(OpenLibreSpotFolder);
        RefreshSnapshotCommand = CreateAsyncCommand(RefreshSnapshotAsync);
        ClearAssetCacheCommand = CreateAsyncCommand(
            () => RunMaintenanceAsync(new MaintenanceActionDefinition(
                "ClearCache",
                L("Vm_ShellClearCacheTitle"),
                L("Vm_ClearCacheActionDescription"),
                L("Vm_ShellClearCacheTitle"))),
            () => !IsRunning);
        RefreshSupportBundlePreviewCommand = new RelayCommand(RefreshSupportBundlePreview);
        ExportSupportBundleCommand = CreateAsyncCommand(ExportSupportBundleAsync, () => !IsRunning);
        ExportFailureBundleCommand = CreateAsyncCommand(ExportFailureBundleAsync, () => CanExportFailureBundle);
        RefreshProfilesCommand = CreateAsyncCommand(() => RefreshLocalProfilesAsync(), () => !IsRunning);
        PreviewSelectedProfileCommand = CreateAsyncCommand(PreviewSelectedProfileAsync, CanUseSelectedProfile);
        ApplySelectedProfileCommand = CreateAsyncCommand(ApplySelectedProfileAsync, CanUseSelectedProfile);
        CreateProfileCommand = CreateAsyncCommand(CreateLocalProfileAsync, CanCreateLocalProfile);
        DuplicateProfileCommand = CreateAsyncCommand(DuplicateLocalProfileAsync, CanUseSelectedProfile);
        RenameProfileCommand = CreateAsyncCommand(RenameLocalProfileAsync, CanRenameLocalProfile);
        DeleteProfileCommand = CreateAsyncCommand(DeleteLocalProfileAsync, CanDeleteLocalProfile);
        ExportProfileCommand = CreateAsyncCommand(ExportLocalProfileAsync, CanUseSelectedProfile);
        ImportProfileCommand = CreateAsyncCommand(ImportLocalProfileAsync, () => !IsRunning);
        CopyProfileShareUriCommand = new RelayCommand(CopyProfileShareUri, () => HasSelectedProfileShareCard);
        CopyProfileComparisonCommand = new RelayCommand(CopyProfileComparison, () => HasSelectedLocalProfile);
        ValidateCustomPatchesCommand = new RelayCommand(ValidateCustomPatches, () => !IsRunning);
        FormatCustomPatchesCommand = new RelayCommand(FormatCustomPatches, () => !IsRunning && !string.IsNullOrWhiteSpace(CustomPatchesJson));
        ClearCustomPatchesCommand = new RelayCommand(ClearCustomPatches, () => !IsRunning && (CustomPatchesEnabled || !string.IsNullOrWhiteSpace(CustomPatchesJson)));
        ImportCustomPatchesFromUrlCommand = CreateAsyncCommand(ImportCustomPatchesFromUrlAsync, () => !IsRunning && !string.IsNullOrWhiteSpace(CustomPatchesImportUrl));
        OpenRepositoryCommand = new RelayCommand(() => OpenExternalUri("https://github.com/SysAdminDoc/LibreSpot"));
        OpenLibreSpotUpdateCommand = new RelayCommand(OpenLibreSpotUpdate, () => HasLibreSpotUpdateNotice);
        OpenSpicetifyCommunityCommand = new RelayCommand(() => OpenExternalUri("https://spicetify.app/docs/advanced-usage/extensions"));
        OpenThemeCatalogCommand = new RelayCommand(() => OpenExternalUri("https://github.com/spicetify/spicetify-themes"));
        ShowRecommendedWorkspaceCommand = new RelayCommand(() => SelectedWorkspaceIndex = 0);
        ShowCustomWorkspaceCommand = new RelayCommand(() => SelectedWorkspaceIndex = 1);
        ShowMaintenanceWorkspaceCommand = new RelayCommand(() => SelectedWorkspaceIndex = 2);
        EnableAutoReapplyCommand = new RelayCommand(() => PresentAutoReapplyPrompt(enable: true), () => !IsRunning && !Snapshot.AutoReapplyTaskRegistered);
        DisableAutoReapplyCommand = new RelayCommand(() => PresentAutoReapplyPrompt(enable: false), () => !IsRunning && Snapshot.AutoReapplyTaskRegistered);
        ClearSettingsSearchCommand = new RelayCommand(() => SettingsSearchText = string.Empty, () => HasSettingsSearchText);
        ClearThemeSearchCommand = new RelayCommand(() => ThemeSearchText = string.Empty, () => HasThemeSearchText);
        ClearFeatureSearchCommand = new RelayCommand(() => FeatureSearchText = string.Empty, () => HasFeatureSearchText);
        ClearGlobalSearchCommand = new RelayCommand(() => GlobalSearchText = string.Empty, () => HasGlobalSearchText);
        FocusGlobalSearchCommand = new RelayCommand(() => GlobalSearchFocusRequested?.Invoke(this, EventArgs.Empty));
        ConfirmPromptCommand = CreateAsyncCommand(ConfirmPromptAsync, () => IsPromptVisible);
        CancelPromptCommand = new RelayCommand(CancelPrompt, () => IsPromptVisible);
        EscapeCommand = new RelayCommand(HandleEscape);

        ConfigureSettingsSearchFilters();
        RegisterOptionStateObservers();
        InitializeSupportBundleItems();
        RefreshSupportBundlePreview();
        RefreshMaintenanceActionRelevance();
        RaiseSelectionInsightsChanged();
        RaiseSnapshotInsightsChanged();
    }

    private string L(string key) => _localizationService.GetString(key);

    private string LF(string key, params object?[] args) =>
        string.Format(_localizationService.Culture, L(key), args);

    public ObservableCollection<string> RecommendedHighlights { get; }
    public ObservableCollection<string> ThemeNames => _customOptions.ThemeNames;
    public ObservableCollection<ThemeGalleryItemViewModel> ThemeGalleryItems => _customOptions.ThemeGalleryItems;
    public ObservableCollection<string> SchemeOptions => _customOptions.SchemeOptions;
    public ObservableCollection<string> LyricsThemes => _customOptions.LyricsThemes;
    public ObservableCollection<AppCatalog.SpotifyVersionEntry> SpotifyVersionOptions => _customOptions.SpotifyVersionOptions;
    public ObservableCollection<AppCatalog.DownloadMethodEntry> DownloadMethodOptions => _customOptions.DownloadMethodOptions;
    public ObservableCollection<SelectionInsightViewModel> SelectionInsights { get; }
    public ObservableCollection<string> SelectedExtensionLabels { get; }

    public ObservableCollection<OptionToggleViewModel> InstallOptions => _customOptions.InstallOptions;
    public ObservableCollection<OptionToggleViewModel> CoreOptions => _customOptions.CoreOptions;
    public ObservableCollection<OptionToggleViewModel> InterfaceOptions => _customOptions.InterfaceOptions;
    public ObservableCollection<OptionToggleViewModel> AdvancedOptions => _customOptions.AdvancedOptions;
    public ObservableCollection<OptionToggleViewModel> ExperienceOptions => _customOptions.ExperienceOptions;
    public ObservableCollection<ExtensionToggleViewModel> Extensions => _customOptions.Extensions;
    public ObservableCollection<ExtensionToggleViewModel> CustomApps => _customOptions.CustomApps;
    public ObservableCollection<CustomizationFeatureOptionViewModel> CustomizationFeatures => _customOptions.CustomizationFeatures;
    public ObservableCollection<CustomizationSnippetToggleViewModel> CustomizationSnippets => _customOptions.CustomizationSnippets;
    public ObservableCollection<CustomizationGroupOption> FeatureGroups => _customOptions.FeatureGroups;
    public ObservableCollection<MaintenanceActionCardViewModel> SafeMaintenanceActions => _maintenanceActions.SafeActions;
    public ObservableCollection<MaintenanceActionCardViewModel> DestructiveMaintenanceActions => _maintenanceActions.DestructiveActions;
    public ObservableCollection<SupportBundleCategoryViewModel> SupportBundleItems { get; }
    public ObservableCollection<string> SupportBundleRedactionRules { get; }
    public ObservableCollection<string> ChangelogHighlights { get; }
    public ObservableCollection<UndoActionItemViewModel> UndoActionItems => _activityState.UndoActionItems;
    public ObservableCollection<LocalProfileCardViewModel> LocalProfiles { get; }
    public ObservableCollection<GlobalSearchResultViewModel> GlobalSearchResults { get; }
    public ObservableCollection<string> CustomPatchFindings { get; }
    public ObservableCollection<LocalizationOption> LocalizationOptions { get; }
    public ObservableCollection<LogEntryViewModel> LogEntries => _activityState.LogEntries;

    public IAsyncRelayCommand ApplyRecommendedCommand { get; }
    public IAsyncRelayCommand ApplyCustomCommand { get; }
    public IAsyncRelayCommand OpenSpotifyCommand { get; }
    public RelayCommand CancelRunCommand { get; }
    public RelayCommand DismissActivityCommand { get; }
    public RelayCommand CopyOperationIdCommand { get; }
    public RelayCommand CopyLogCommand { get; }
    public RelayCommand ClearLogCommand { get; }
    public RelayCommand PreviewSelectedUndoCommand { get; }
    public RelayCommand ExecuteSelectedUndoCommand { get; }
    public RelayCommand CycleShellLogFilterCommand { get; }
    public RelayCommand OpenLibreSpotFolderCommand { get; }
    public IAsyncRelayCommand RefreshSnapshotCommand { get; }
    public IAsyncRelayCommand ClearAssetCacheCommand { get; }
    public RelayCommand RefreshSupportBundlePreviewCommand { get; }
    public IAsyncRelayCommand ExportSupportBundleCommand { get; }
    public IAsyncRelayCommand ExportFailureBundleCommand { get; }
    public IAsyncRelayCommand RefreshProfilesCommand { get; }
    public IAsyncRelayCommand PreviewSelectedProfileCommand { get; }
    public IAsyncRelayCommand ApplySelectedProfileCommand { get; }
    public IAsyncRelayCommand CreateProfileCommand { get; }
    public IAsyncRelayCommand DuplicateProfileCommand { get; }
    public IAsyncRelayCommand RenameProfileCommand { get; }
    public IAsyncRelayCommand DeleteProfileCommand { get; }
    public IAsyncRelayCommand ExportProfileCommand { get; }
    public IAsyncRelayCommand ImportProfileCommand { get; }
    public RelayCommand CopyProfileShareUriCommand { get; }
    public RelayCommand CopyProfileComparisonCommand { get; }
    public RelayCommand ValidateCustomPatchesCommand { get; }
    public RelayCommand FormatCustomPatchesCommand { get; }
    public RelayCommand ClearCustomPatchesCommand { get; }
    public IAsyncRelayCommand ImportCustomPatchesFromUrlCommand { get; }
    public RelayCommand OpenRepositoryCommand { get; }
    public RelayCommand OpenSpicetifyCommunityCommand { get; }
    public RelayCommand OpenThemeCatalogCommand { get; }
    public RelayCommand ShowRecommendedWorkspaceCommand { get; }
    public RelayCommand ShowCustomWorkspaceCommand { get; }
    public RelayCommand ShowMaintenanceWorkspaceCommand { get; }
    public RelayCommand EnableAutoReapplyCommand { get; }
    public RelayCommand DisableAutoReapplyCommand { get; }
    public RelayCommand ClearSettingsSearchCommand { get; }
    public RelayCommand ClearThemeSearchCommand { get; }
    public RelayCommand ClearFeatureSearchCommand { get; }
    public RelayCommand ClearGlobalSearchCommand { get; }
    public RelayCommand FocusGlobalSearchCommand { get; }
    public IAsyncRelayCommand ConfirmPromptCommand { get; }
    public RelayCommand CancelPromptCommand { get; }
    public RelayCommand EscapeCommand { get; }

    public event EventHandler? GlobalSearchFocusRequested;

    public EnvironmentSnapshot Snapshot => _environmentState.Snapshot;

    public LocalizationOption SelectedLocalizationOption
    {
        get => _selectedLocalizationOption;
        set
        {
            if (value is null)
            {
                return;
            }

            if (SetProperty(ref _selectedLocalizationOption, value))
            {
                _localizationService.ApplyCulture(value.CultureName);
                if (!_applyingCultureFromConfig)
                {
                    _ = PersistUiCultureAsync(value.CultureName);
                }
            }
        }
    }

    public bool IsAdministratorSession => _isAdministratorSession;


    public string ShellReadinessTitle => L("Vm_ShellReadinessTitle");

    public string ShellReadinessValue =>
        IsSnapshotLoading
            ? L("Vm_ShellCheckingSystem")
            : HasSnapshotLoadError
                ? L("Vm_ShellSnapshotUnavailable")
            : HasCriticalHealthIssues
                ? Strings.RunNeedsAttention
                : L("Vm_ShellReadyToPatch");

    public string ShellReadinessDetail =>
        IsSnapshotLoading
            ? L("Vm_ShellCheckingSystemDetail")
            : HasSnapshotLoadError
                ? L("Vm_ShellSnapshotUnavailableDetail")
            : HasCriticalHealthIssues
                ? HealthIssueSummary
                : L("Vm_ShellNoBlockingIssues");

    public string ShellReadinessPercent
    {
        get
        {
            if (IsSnapshotLoading || HasSnapshotLoadError)
            {
                return "—";
            }

            var checks = ShellReadinessChecks;
            var passed = checks.Count(check => check.IsPassing);
            return $"{(int)Math.Round(passed * 100.0 / checks.Count)}%";
        }
    }

    public string ShellReadinessShortLabel =>
        IsSnapshotLoading
            ? L("Vm_ShellCheckingShort")
            : HasSnapshotLoadError
                ? L("Vm_ShellRetryShort")
                : HasCriticalHealthIssues
                    ? L("Vm_ShellNeedsReviewShort")
                    : Strings.SeverityReady;

    public bool IsSnapshotLoading => _isSnapshotLoading;
    public bool HasSnapshotLoadError => _snapshotLoadFailed;
    public bool IsEnvironmentReadyForActions => !IsSnapshotLoading && !HasSnapshotLoadError;
    public HomeActionViewModel HomeAction => BuildHomeAction();
    public HomeActionViewModel MaintenanceRecommendation => BuildMaintenanceRecommendation();

    public string MaintenanceOverallStatus =>
        IsSnapshotLoading
            ? L("Vm_ShellCheckingShort")
            : HasSnapshotLoadError
                ? L("Vm_ShellSnapshotUnavailable")
                : HealthReport.HasCriticalIssues
                    ? Strings.SeverityCritical
                    : HealthReport.WarningIssues.Count > 0
                        ? Strings.SeverityWarning
                        : Strings.SeverityReady;

    public string MaintenanceOverallTone =>
        IsSnapshotLoading
            ? HealthSeverity.Info
            : HasSnapshotLoadError
                ? HealthSeverity.Warning
                : HealthReport.HasCriticalIssues
                    ? HealthSeverity.Critical
                    : HealthReport.WarningIssues.Count > 0
                        ? HealthSeverity.Warning
                        : HealthSeverity.Ready;

    public IReadOnlyList<ShellReadinessCheckItemViewModel> SimpleHomeReadinessChecks
    {
        get
        {
            var checks = ShellReadinessChecks;
            var labels = new[]
            {
                L("Vm_SimpleHomeSystem"),
                L("Vm_SimpleHomeSpotify"),
                L("Vm_SimpleHomePermissions"),
                L("Vm_SimpleHomeDependencies")
            };

            return checks
                .Select((check, index) => new ShellReadinessCheckItemViewModel(
                    labels[index],
                    check.Status,
                    check.Tone,
                    check.IsPassing))
                .ToArray();
        }
    }

    public string ShellQuickActionsTitle => L("Vm_ShellQuickActionsTitle");
    public string ShellNextActionsTitle => L("Vm_ShellNextActionsTitle");
    public string ShellActionRunSetupTitle => L("Vm_ShellActionRunSetupTitle");
    public string ShellActionRunSetupDetail => L("Vm_ShellActionRunSetupDetail");
    public string ShellActionUnblockTitle => L("Vm_ShellActionUnblockTitle");
    public string ShellActionUnblockDetail => L("Vm_ShellActionUnblockDetail");
    public string ShellActionToolsTitle => L("Vm_ShellActionToolsTitle");
    public string ShellActionToolsDetail => L("Vm_ShellActionToolsDetail");
    public string ShellSystemChecksLabel => L("Vm_ShellSystemChecksLabel");
    public string ShellSpotifyDetectedLabel => L("Vm_ShellSpotifyDetectedLabel");
    public string ShellWritePermissionsLabel => L("Vm_ShellWritePermissionsLabel");
    public string ShellDependenciesLabel => L("Vm_ShellDependenciesLabel");
    public string ShellCheckOkLabel => L("Vm_ShellCheckOkLabel");
    public IReadOnlyList<ShellReadinessCheckItemViewModel> ShellReadinessChecks
    {
        get
        {
            if (IsSnapshotLoading)
            {
                return
                [
                    new(ShellSystemChecksLabel, L("Vm_ShellCheckingShort"), HealthSeverity.Info, false),
                    new(ShellSpotifyDetectedLabel, L("Vm_ShellCheckingShort"), HealthSeverity.Info, false),
                    new(ShellWritePermissionsLabel, L("Vm_ShellCheckingShort"), HealthSeverity.Info, false),
                    new(ShellDependenciesLabel, L("Vm_ShellCheckingShort"), HealthSeverity.Info, false)
                ];
            }

            if (HasSnapshotLoadError)
            {
                return
                [
                    new(ShellSystemChecksLabel, L("Vm_ShellRetryShort"), HealthSeverity.Warning, false),
                    new(ShellSpotifyDetectedLabel, Strings.DashboardUnknownValue, HealthSeverity.Info, false),
                    new(ShellWritePermissionsLabel, Strings.DashboardUnknownValue, HealthSeverity.Info, false),
                    new(ShellDependenciesLabel, Strings.DashboardUnknownValue, HealthSeverity.Info, false)
                ];
            }

            var systemPassing = !HasCriticalHealthIssues;
            var dependencyRows = ShellDependencyRows.Take(3).ToArray();
            var dependenciesPassing = dependencyRows.All(row => row.Tone != HealthSeverity.Critical);
            var dependenciesInstalled = dependencyRows.All(row => row.Tone == HealthSeverity.Ready);

            return
            [
                new(
                    ShellSystemChecksLabel,
                    systemPassing ? ShellCheckOkLabel : L("Vm_ShellNeedsReviewShort"),
                    systemPassing ? HealthSeverity.Ready : HealthSeverity.Critical,
                    systemPassing),
                new(
                    ShellSpotifyDetectedLabel,
                    Snapshot.SpotifyInstalled ? L("Vm_ShellSpotifyInstalled") : L("Vm_ShellSpotifyNotDetected"),
                    Snapshot.SpotifyInstalled ? HealthSeverity.Ready : HealthSeverity.Info,
                    true),
                new(
                    ShellWritePermissionsLabel,
                    ShellCheckOkLabel,
                    HealthSeverity.Ready,
                    true),
                new(
                    ShellDependenciesLabel,
                    dependenciesInstalled ? ShellCheckOkLabel : ShellReadyText,
                    dependenciesPassing ? (dependenciesInstalled ? HealthSeverity.Ready : HealthSeverity.Info) : HealthSeverity.Critical,
                    dependenciesPassing)
            ];
        }
    }
    public string ShellVerifyEnvironmentTitle => L("Vm_ShellVerifyEnvironmentTitle");
    public string ShellVerifyEnvironmentDetail => L("Vm_ShellVerifyEnvironmentDetail");
    public string ShellRepairTitle => L("Vm_ShellRepairTitle");
    public string ShellRepairDetail => L("Vm_ShellRepairDetail");
    public string ShellClearCacheTitle => L("Vm_ShellClearCacheTitle");
    public string ShellClearCacheDetail => L("Vm_ShellClearCacheDetail");
    public string ShellTrustRiskTitle => L("Vm_ShellTrustRiskTitle");
    public string ShellTrustedSourcesTitle => L("Vm_ShellTrustedSourcesTitle");
    public string ShellTrustedSourcesDetail => L("Vm_ShellTrustedSourcesDetail");
    public string ShellProvenanceTitle => L("Vm_ShellProvenanceTitle");
    public string ShellProvenanceDetail => L("Vm_ShellProvenanceDetail");
    public IReadOnlyList<ProvenanceItemViewModel> ShellProvenanceItems =>
        AppCatalog.UpstreamDependencyPins.Select(BuildProvenanceItem).ToArray();
    public string ShellSpotifyModificationTitle => L("Vm_ShellSpotifyModificationTitle");
    public string ShellSpotifyModificationDetail => L("Vm_ShellSpotifyModificationDetail");
    public string ShellBackupCreatedTitle => L("Vm_ShellBackupCreatedTitle");
    public string ShellBackupCreatedDetail => Snapshot.SavedConfigExists
        ? L("Vm_ShellBackupCreatedSaved")
        : L("Vm_ShellBackupCreatedPending");
    public string ShellActivityTitle => L("Vm_ShellActivityTitle");
    public string ShellNoActiveTasksText => IsRunning ? ActivityStatus : L("Vm_ShellNoActiveTasks");
    public string ShellReadyText => Strings.SeverityReady;
    public string ShellServiceStatusText => Snapshot.SpotifyInstalled || Snapshot.SpicetifyInstalled
        ? L("Vm_ShellServiceDetected")
        : L("Vm_ShellServiceStandby");
    private static string ProductVersion =>
        typeof(MainViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(MainViewModel).Assembly.GetName().Version?.ToString()
        ?? "unknown";
    public string ShellDisplayVersion => $"v{ProductVersion}";

    // A newer stable release shows one quiet inline link under the primary
    // action. It never replaces the action, raises a toast, or takes focus.
    public bool HasLibreSpotUpdateNotice => _libreSpotUpdateNotice is { UpdateAvailable: true };
    public string LibreSpotUpdateNoticeText => _libreSpotUpdateNotice is { UpdateAvailable: true } notice
        ? LF("Vm_LibreSpotUpdateNoticeFormat", notice.LatestVersion)
        : string.Empty;
    public string LibreSpotUpdateNoticeLinkLabel => L("Vm_LibreSpotUpdateNoticeLink");
    public string LibreSpotUpdateNoticeAutomationName => HasLibreSpotUpdateNotice
        ? $"{LibreSpotUpdateNoticeLinkLabel}. {LibreSpotUpdateNoticeText}"
        : string.Empty;
    public ICommand OpenLibreSpotUpdateCommand { get; }
    public Task LibreSpotUpdateCheck => _libreSpotUpdateCheck ?? Task.CompletedTask;
    public string ShellStackStatusTitle => Snapshot.SpicetifyInstalled || Snapshot.SpotifyInstalled
        ? L("Vm_ShellStackDetectedTitle")
        : L("Vm_ShellStackNotDetectedTitle");
    public string ShellStackStatusDetail => Snapshot.SpicetifyInstalled || Snapshot.SpotifyInstalled
        ? L("Vm_ShellStackDetectedDetail")
        : L("Vm_ShellStackNotDetectedDetail");
    public string ShellTopThemeLabel => L("Vm_ShellTopThemeLabel");
    public string ShellTopSettingsLabel => L("Vm_ShellTopSettingsLabel");
    public string ShellLearnMoreLabel => L("Vm_ShellLearnMoreLabel");
    public string ShellLogLevelLabel => _shellLogFilterIndex switch
    {
        1 => L("Vm_ShellLogWarningsAndErrors"),
        2 => L("Vm_ShellLogErrorsOnly"),
        _ => L("Vm_ShellLogLevelLabel")
    };
    public string ShellClearLogLabel => L("Vm_ShellClearLogLabel");
    public string ShellClearLogHint => L("Vm_ShellClearLogHint");
    public string ShellLogFilterHint => L("Vm_ShellLogFilterHint");
    public string ShellActivityEmptyTitle => LogEntries.Count == 0 ? ShellNoActiveTasksText : ShellLogLevelLabel;
    public string ShellActivityEmptyDetail => LogEntries.Count == 0
        ? L("Vm_ShellActivityEmptyDetail")
        : L("Vm_ShellActivityFilterEmptyDetail");
    public string ShellAutoScrollLabel => L("Vm_ShellAutoScrollLabel");
    public string ShellRunRecommendedCaption => L("Vm_ShellRunRecommendedCaption");
    public bool ShowRecommendedRunBand => SelectedWorkspaceIndex == 0;
    public string ShellActiveRunTitle => L("Vm_ShellActiveRunTitle");
    public string ShellLocalEnvironmentTitle => L("Vm_ShellLocalEnvironmentTitle");
    public string ShellDependenciesTitle => L("Vm_ShellDependenciesTitle");
    public string ShellDependencyComponentHeader => L("Vm_ShellDependencyComponentHeader");
    public string ShellDependencyInstalledHeader => L("Vm_ShellDependencyInstalledHeader");
    public string ShellDependencyRecommendedHeader => L("Vm_ShellDependencyRecommendedHeader");
    public string ShellDependencyStatusHeader => L("Vm_ShellDependencyStatusHeader");
    public string ShellEnvironmentReportLinkText => L("Vm_ShellEnvironmentReportLinkText");
    public string ShellDependenciesSummaryText => ShellDependencyRows.Any(row => row.Tone == HealthSeverity.Critical || row.Tone == HealthSeverity.Warning)
        ? L("Vm_ShellDependenciesWarning")
        : L("Vm_ShellDependenciesHealthy");

    private string ShellSpotifyTargetDetail
    {
        get
        {
            if (!Snapshot.SpotifyInstalled)
            {
                return L("Vm_ShellSpotifyTargetPerUserPath");
            }

            if (Environment.GetCommandLineArgs().Any(arg => arg.StartsWith("--uia-smoke=", StringComparison.OrdinalIgnoreCase)))
            {
                return @"C:\Program Files\Spotify";
            }

            var path = HealthComponent("spotify")?.Path;
            if (string.IsNullOrWhiteSpace(path))
            {
                return L("Vm_ShellSpotifyTargetDetectedPath");
            }

            return Path.GetDirectoryName(path) ?? path;
        }
    }

    public IReadOnlyList<ShellSummaryItemViewModel> ShellSummaryItems =>
    [
        new(L("Vm_ShellSummaryStatus"), ShellReadinessValue, ShellReadinessDetail, "status", ShellReadinessTone),
        new(
            L("Vm_ShellSpotifyTargetLabel"),
            Snapshot.SpotifyInstalled
                ? FirstNonEmpty(HealthComponent("spotify")?.DetectedVersion, ShellSpotifyTargetDetail)
                : L("Vm_ShellSpotifyNotDetected"),
            SpotifyStatusLine,
            "spotify",
            Snapshot.SpotifyInstalled ? HealthSeverity.Ready : HealthSeverity.Info),
        new(
            Strings.DashboardSpicetifyVersionLabel,
            Snapshot.SpicetifyInstalled
                ? FirstNonEmpty(HealthComponent("spicetify-cli")?.DetectedVersion, AppCatalog.PinnedSpicetifyCliVersion)
                : L("Vm_SpicetifyNotInstalled"),
            CustomizationStatusLine,
            "spicetify",
            HealthComponent("spicetify-cli")?.Severity
                ?? (Snapshot.SpicetifyInstalled ? HealthSeverity.Ready : HealthSeverity.Info)),
        new(
            Strings.MarketplaceLabel,
            Snapshot.MarketplaceReady ? AppCatalog.PinnedMarketplaceVersion : MarketplaceStatusLine,
            MarketplaceStatusLine,
            "marketplace",
            HealthComponent("marketplace")?.Severity
                ?? (Snapshot.MarketplaceReady ? HealthSeverity.Ready : HealthSeverity.Info))
    ];

    public IReadOnlyList<ShellEnvironmentRowViewModel> ShellEnvironmentRows =>
    [
        new(L("Vm_EnvUser"), Environment.UserName, HealthSeverity.Ready),
        new(L("Vm_EnvMachine"), Environment.MachineName, HealthSeverity.Ready),
        new(L("Vm_EnvWorkingDirectory"), Environment.CurrentDirectory, HealthSeverity.Ready),
        new(L("Vm_EnvPermissions"), IsAdministratorSession ? L("Vm_EnvAdministrator") : L("Vm_EnvStandardUser"), HealthSeverity.Ready)
    ];

    public IReadOnlyList<ShellDependencyRowViewModel> ShellDependencyRows =>
    [
        BuildDependencyRow("Spicetify CLI", HealthComponent("spicetify-cli"), AppCatalog.PinnedSpicetifyCliVersion),
        BuildDependencyRow("SpotX (core)", HealthComponent("spotx"), AppCatalog.PinnedSpotXVersion),
        BuildDependencyRow("Marketplace", HealthComponent("marketplace"), AppCatalog.PinnedMarketplaceVersion),
        new(
            "Spotify",
            FirstNonEmpty(HealthComponent("spotify")?.DetectedVersion, ShellSpotifyTargetDetail),
            L("Vm_ShellSpotifyInstalled"),
            Snapshot.SpotifyInstalled ? HealthSeverity.Ready : HealthSeverity.Warning)
    ];

    public IReadOnlyList<LogEntryViewModel> ShellActivityLogItems =>
        LogEntries.Where(IsShellLogEntryVisible).ToArray();

    public bool HasShellActivityLogItems => ShellActivityLogItems.Count > 0;
    public bool ShowShellActivityEmptyState => !HasShellActivityLogItems;
    public bool IsShellInteractionEnabled => !IsActivityVisible && !IsPromptVisible;

    private string ShellReadinessTone =>
        IsSnapshotLoading
            ? HealthSeverity.Info
            : HasSnapshotLoadError
                ? HealthSeverity.Warning
                : HasCriticalHealthIssues
                    ? HealthSeverity.Critical
                    : HealthSeverity.Ready;

    public string SpotifyStatusLine =>
        Snapshot.SpotifyInstalled
            ? L("Vm_SpotifyDetected")
            : L("Vm_SpotifyNotInstalled");

    public string CustomizationStatusLine =>
        Snapshot.SpicetifyInstalled
            ? L("Vm_SpicetifyDetected")
            : L("Vm_SpicetifyNotInstalled");

    public string MarketplaceStatusLine =>
        !Snapshot.SpicetifyInstalled
            ? L("Vm_MarketplaceUnavailable")
            : Snapshot.MarketplaceReady
                ? L("Vm_MarketplaceReady")
                : Snapshot.MarketplaceFilesPresent
                    ? L("Vm_MarketplaceHidden")
                    : Snapshot.MarketplaceRegistered
                        ? L("Vm_MarketplaceFilesMissing")
                        : L("Vm_MarketplaceNotEnabled");

    public StackHealthReport HealthReport => Snapshot.HealthReport;
    public IReadOnlyList<HealthIssueViewModel> CriticalHealthIssues => BuildHealthIssues(HealthReport.CriticalIssues);
    public IReadOnlyList<HealthIssueViewModel> WarningHealthIssues => BuildHealthIssues(HealthReport.WarningIssues);
    public IReadOnlyList<HealthIssueViewModel> InfoHealthIssues => BuildHealthIssues(HealthReport.InfoIssues);
    public bool HasCriticalHealthIssues => HealthReport.HasCriticalIssues;
    public string HealthIssueSummary => HealthReport.IssueSummary;
    public bool HasUndoActionItems => _activityState.HasUndoActionItems;
    public bool HasExecutableUndoActionItems => _activityState.HasExecutableUndoActionItems;

    public IReadOnlyList<StatusDashboardItemViewModel> StatusDashboardItems =>
    [
        BuildDashboardItem(
            Strings.DashboardSpotifyVersionLabel,
            HealthComponent("spotify"),
            component => FirstNonEmpty(component.DetectedVersion, component.Status)),
        BuildDashboardItem(
            Strings.DashboardSpicetifyVersionLabel,
            HealthComponent("spicetify-cli"),
            component => FirstNonEmpty(component.DetectedVersion, component.Status)),
        BuildDashboardItem(
            Strings.DashboardSpotXStateLabel,
            HealthComponent("spotx"),
            component => component.Status),
        BuildLastPatchDashboardItem(),
        BuildDashboardItem(
            Strings.DashboardWatcherLabel,
            HealthComponent("auto-reapply-watcher"),
            component => component.Status),
        BuildDashboardItem(
            Strings.BackupsLabel,
            HealthComponent("backups"),
            component => component.Status)
    ];

    public IReadOnlyList<StatusDashboardItemViewModel> ShellPrimaryStatusItems =>
        StatusDashboardItems.Take(3).ToArray();

    public IReadOnlyList<CompatibilityVerdictItemViewModel> CompatibilityVerdictItems =>
        Snapshot.CompatibilityVerdicts.Items
            .Select(BuildCompatibilityVerdictItem)
            .ToArray();

    public string CompatibilityVerdictSummary =>
        LF(
            "CompatibilityVerdictSummaryFormat",
            LocalizeCompatibilityVerdict(Snapshot.CompatibilityVerdicts.OverallVerdict));

    public bool HasConfigurationRecoveryNotice =>
        _configurationLoadState == ConfigurationLoadState.RecoveredFromCorrupt;

    private bool IsForwardIncompatibleConfiguration =>
        _configurationRecoveryReason?.Contains("newer than this LibreSpot build supports", StringComparison.OrdinalIgnoreCase) == true;

    public string ConfigurationRecoveryTitle =>
        IsForwardIncompatibleConfiguration
            ? L("Vm_ConfigRecoveryNewerTitle")
            : L("Vm_ConfigRecoveryRecoveredTitle");

    private string ConfigurationRecoveryReasonClause =>
        string.IsNullOrWhiteSpace(_configurationRecoveryReason)
            ? string.Empty
            : LF("Vm_ConfigRecoveryReasonFormat", _configurationRecoveryReason.Trim());

    public string ConfigurationRecoveryDetail =>
        !HasConfigurationRecoveryNotice
            ? string.Empty
            : IsForwardIncompatibleConfiguration
                ? string.IsNullOrWhiteSpace(_recoveredConfigurationPath)
                    ? LF("Vm_ConfigRecoveryNewerNoBackupFormat", ConfigurationRecoveryReasonClause)
                    : LF("Vm_ConfigRecoveryNewerBackupFormat", ConfigurationRecoveryReasonClause, Path.GetFileName(_recoveredConfigurationPath))
            : string.IsNullOrWhiteSpace(_recoveredConfigurationPath)
                ? LF("Vm_ConfigRecoveryUnreadableNoBackupFormat", ConfigurationRecoveryReasonClause)
                : LF("Vm_ConfigRecoveryUnreadableBackupFormat", ConfigurationRecoveryReasonClause, Path.GetFileName(_recoveredConfigurationPath));

    public string ProfileStatusLine =>
        HasConfigurationRecoveryNotice
            ? L("Vm_ProfileRecoveredDefaults")
            : Snapshot.SavedConfigExists
            ? L("Vm_ProfileSavedFound")
            : L("Vm_ProfileNoSavedProfile");

    public string AutoReapplyStatusTitle =>
        Snapshot.AutoReapplyTaskRegistered
            ? L("Vm_AutoReapplyActiveTitle")
            : L("Vm_AutoReapplyOffTitle");

    public string AutoReapplyStatusDetail =>
        Snapshot.AutoReapplyTaskRegistered
            ? L("Vm_AutoReapplyActiveDetail")
            : L("Vm_AutoReapplyOffDetail");

    public string AutoReapplyTaskLine =>
        Snapshot.AutoReapplyTaskRegistered
            ? L("Vm_AutoReapplyTaskRegistered")
            : L("Vm_AutoReapplyTaskNotRegistered");

    public string AutoReapplyLogLine =>
        LF("Vm_AutoReapplyLogFormat", Path.Combine(_configurationService.ConfigDirectory, "watcher.log"));

    public string WorkspaceRecommendationTitle =>
        HasConfigurationRecoveryNotice
            ? L("Vm_WorkspaceRecommendationRecoverTitle")
            : Snapshot.SpotifyInstalled && Snapshot.SpicetifyInstalled
            ? L("Vm_WorkspaceRecommendationMaintainTitle")
            : Snapshot.SpotifyInstalled
                ? L("Vm_WorkspaceRecommendationFinishTitle")
                : L("Vm_WorkspaceRecommendationStartTitle");

    public string WorkspaceRecommendationBrief =>
        HasConfigurationRecoveryNotice
            ? L("Vm_WorkspaceRecommendationRecoverBrief")
            : Snapshot.SpotifyInstalled && Snapshot.SpicetifyInstalled
                ? L("Vm_WorkspaceRecommendationMaintainBrief")
                : Snapshot.SpotifyInstalled
                    ? L("Vm_WorkspaceRecommendationFinishBrief")
                    : L("Vm_WorkspaceRecommendationStartBrief");

    public string CustomSelectionSummary
    {
        get
        {
            var changeCount = CountProfileDifferencesFromRecommended();
            return changeCount switch
            {
                0 => L("Vm_CustomSelectionAligned"),
                1 => L("Vm_CustomSelectionOneChange"),
                _ => LF("Vm_CustomSelectionManyChangesFormat", changeCount)
            };
        }
    }

    public string InstallPostureLabel =>
        IsOptionSelected(nameof(InstallConfiguration.CleanInstall))
            ? L("Vm_InstallPostureClean")
            : L("Vm_InstallPostureOverlay");

    public string CustomChangeCountLabel
    {
        get
        {
            var changeCount = CountProfileDifferencesFromRecommended();
            return changeCount switch
            {
                0 => L("Vm_CustomChangeMatchesRecommended"),
                1 => L("Vm_CustomChangeOne"),
                _ => LF("Vm_CustomChangeManyFormat", changeCount)
            };
        }
    }

    public string SelectedExtensionCountLabel
    {
        get
        {
            var selectedCount = Extensions.Count(item => item.IsSelected);
            return selectedCount switch
            {
                0 => L("Vm_CountNone"),
                1 => L("Vm_CountOneSelected"),
                _ => LF("Vm_CountSelectedFormat", selectedCount)
            };
        }
    }

    public string CustomAppsSectionTitle => L("Vm_CustomAppsSectionTitle");

    public string CustomAppsSectionDescription =>
        L("Vm_CustomAppsSectionDescription");

    public string AccessPostureLabel => L("Vm_AccessPostureCurrentSession");

    public bool HasSelectedExtensions => SelectedExtensionLabels.Count > 0;

    public string ThemeSummary =>
        SelectedTheme == "(None - Marketplace Only)"
            ? L("Vm_ThemeMarketplaceOnly")
            : LF("Vm_ThemeSummaryFormat", SelectedTheme, Prettify.Label(SelectedScheme));

    public bool IsThemeSchemeAvailable => !string.Equals(SelectedTheme, "(None - Marketplace Only)", StringComparison.Ordinal);

    public string ThemeSchemeHint =>
        IsThemeSchemeAvailable
            ? L("Vm_ThemeSchemeAvailableHint")
            : L("Vm_ThemeSchemeMarketplaceOnlyHint");

    public string LyricsSummary => LF("Vm_LyricsSummaryFormat", Prettify.Label(SelectedLyricsTheme));

    public bool IsLyricsThemeAvailable => IsOptionSelected(nameof(InstallConfiguration.SpotX_LyricsEnabled));

    public string LyricsThemeHint =>
        IsLyricsThemeAvailable
            ? L("Vm_LyricsThemeAvailableHint")
            : L("Vm_LyricsThemeUnavailableHint");

    public string CacheSummary =>
        int.TryParse(CacheLimitText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? LF("Vm_CacheCeilingFormat", parsed)
            : L("Vm_CacheDefault");

    public string SpotifyVersionSummary => CurrentSpotifyVersionEntry.Label;

    public string SpotifyVersionNotes => CurrentSpotifyVersionEntry.Notes;

    public string? ArchitectureMismatchWarning =>
        AppCatalog.CheckArchitectureCompatibility(CurrentSpotifyVersionEntry, Snapshot.HostArchitecture);

    public bool HasArchitectureMismatch => !string.IsNullOrEmpty(ArchitectureMismatchWarning);

    public string DownloadMethodSummary => CurrentDownloadMethodEntry.Label;

    public string DownloadMethodDetail => CurrentDownloadMethodEntry.Detail;

    public string SettingsSearchText
    {
        get => _settingsSearch.Text;
        set => _settingsSearch.Text = value;
    }

    public bool HasSettingsSearchText => _settingsSearch.HasText;

    public string GlobalSearchText
    {
        get => _globalSearchText;
        set
        {
            if (SetProperty(ref _globalSearchText, value ?? string.Empty))
            {
                RefreshGlobalSearch();
            }
        }
    }

    public bool HasGlobalSearchText => !string.IsNullOrWhiteSpace(GlobalSearchText);
    public bool HasGlobalSearchResults => GlobalSearchResults.Count > 0;
    public bool ShowGlobalSearchEmptyState => HasGlobalSearchText && !HasGlobalSearchResults;
    public string GlobalSearchSummary => GlobalSearchResults.Count switch
    {
        0 => LF("Vm_GlobalSearchNoMatchesFormat", GlobalSearchText.Trim()),
        1 => LF("Vm_GlobalSearchOneMatchFormat", GlobalSearchText.Trim()),
        _ => LF("Vm_GlobalSearchManyMatchesFormat", GlobalSearchResults.Count, GlobalSearchText.Trim())
    };

    public bool HasVisibleInstallOptions => HasVisibleOptions(InstallOptions);

    public bool HasVisibleAppearanceSettings => CountAppearanceMatches() > 0;

    public bool HasVisibleCoreOptions => HasVisibleOptions(CoreOptions);

    public bool HasVisibleInterfaceOptions => HasVisibleOptions(InterfaceOptions);

    public bool HasVisibleBehaviorSection => HasVisibleCoreOptions || HasVisibleInterfaceOptions;

    public bool HasVisibleAdvancedOptions => HasVisibleOptions(AdvancedOptions);

    public bool HasVisibleExperienceOptions => HasVisibleOptions(ExperienceOptions);

    public bool HasVisibleAdvancedSection => HasVisibleAdvancedOptions || HasVisibleCustomPatchesSection;

    public bool HasVisibleExtensions => Extensions.Any(extension => MatchesSettingsSearch(extension.Title, extension.Description));

    public bool HasVisibleCustomApps => CustomApps.Any(app => MatchesSettingsSearch(app.Title, app.Description));

    public bool HasVisibleLiveCustomization =>
        MatchesSettingsSearch(L("LiveCustomizationTitle"), L("LiveCustomizationDescription")) ||
        CustomizationFeatures.Any(feature => MatchesSettingsSearch(feature.Name, feature.Description)) ||
        CustomizationSnippets.Any(snippet => MatchesSettingsSearch(snippet.Title, snippet.Description));

    public int CustomSearchMatchCount =>
        CountMatchingOptions(InstallOptions) +
        CountAppearanceMatches() +
        CountMatchingOptions(CoreOptions) +
        CountMatchingOptions(InterfaceOptions) +
        CountMatchingOptions(AdvancedOptions) +
        CountMatchingOptions(ExperienceOptions) +
        (HasVisibleCustomPatchesSection ? 1 : 0) +
        Extensions.Count(extension => MatchesSettingsSearch(extension.Title, extension.Description)) +
        CustomApps.Count(app => MatchesSettingsSearch(app.Title, app.Description)) +
        CustomizationFeatures.Count(feature => MatchesSettingsSearch(feature.Name, feature.Description)) +
        CustomizationSnippets.Count(snippet => MatchesSettingsSearch(snippet.Title, snippet.Description));

    public bool HasAnyCustomSearchMatches => !HasSettingsSearchText || CustomSearchMatchCount > 0;

    public bool ShowCustomSearchEmptyState => HasSettingsSearchText && !HasAnyCustomSearchMatches;

    public string CustomSearchSummary =>
        HasSettingsSearchText
            ? CustomSearchMatchCount switch
            {
                0 => LF("Vm_CustomSearchNoMatchesFormat", SettingsSearchText.Trim()),
                1 => LF("Vm_CustomSearchOneMatchFormat", SettingsSearchText.Trim()),
                _ => LF("Vm_CustomSearchManyMatchesFormat", CustomSearchMatchCount, SettingsSearchText.Trim())
            }
            : L("Vm_CustomSearchDefaultSummary");

    private AppCatalog.SpotifyVersionEntry CurrentSpotifyVersionEntry =>
        SpotifyVersionOptions.FirstOrDefault(entry => string.Equals(entry.Id, SelectedSpotifyVersionId, StringComparison.Ordinal))
        ?? SpotifyVersionOptions.First();

    private AppCatalog.DownloadMethodEntry CurrentDownloadMethodEntry =>
        DownloadMethodOptions.FirstOrDefault(entry => string.Equals(entry.Id, SelectedDownloadMethod, StringComparison.Ordinal))
        ?? DownloadMethodOptions.First();

    public string ExtensionSummary
    {
        get
        {
            var selectedCount = Extensions.Count(item => item.IsSelected);
            return selectedCount switch
            {
                0 => L("Vm_ExtensionNoneSelected"),
                1 => L("Vm_ExtensionOneSelected"),
                _ => LF("Vm_ExtensionManySelectedFormat", selectedCount)
            };
        }
    }

    public string MaintenanceGuidanceTitle => MaintenanceRecommendation.Title;

    public string MaintenanceGuidanceDetail => MaintenanceRecommendation.Body;

    private int MaintenanceReadyComponentCount =>
        new[] { "spotify", "spotx", "spicetify-cli", "marketplace", "librespot-live-engine", "active-theme" }
            .Count(id => HealthComponent(id)?.Severity == HealthSeverity.Ready);

    public string MaintenanceReadinessValue => LF("Vm_MaintenanceReadinessValueFormat", MaintenanceReadyComponentCount);

    public string MaintenanceReadinessDetail =>
        MaintenanceReadyComponentCount switch
        {
            6 => L("Vm_MaintenanceReadinessAllReady"),
            0 => L("Vm_MaintenanceReadinessNoneReady"),
            _ => L("Vm_MaintenanceReadinessPartial")
        };

    public string MaintenanceBackupValue => HealthComponent("backups")?.Status ?? Strings.DashboardUnknownValue;

    public string MaintenanceBackupDetail
    {
        get
        {
            var backups = HealthComponent("backups");
            if (backups is null)
            {
                return L("Vm_MaintenanceBackupUnchecked");
            }

            return backups.HasLastChanged
                ? LF("Vm_MaintenanceBackupLatestFormat", backups.Evidence, backups.LastChangedDisplay)
                : backups.Evidence;
        }
    }

    public string MaintenanceMarketplaceValue => HealthComponent("marketplace")?.Status ?? Strings.DashboardUnknownValue;
    public string MaintenanceMarketplaceDetail => HealthComponent("marketplace")?.Evidence ?? L("Vm_MaintenanceMarketplaceUnchecked");
    public string MaintenanceThemeValue => HealthComponent("active-theme")?.Status ?? Strings.DashboardUnknownValue;
    public string MaintenanceThemeDetail => HealthComponent("active-theme")?.Evidence ?? L("Vm_MaintenanceThemeUnchecked");

    public string SupportBundlePreviewTitle =>
        _supportBundlePreview.SelectedFileCount switch
        {
            0 => L("Vm_SupportBundleHealthOnly"),
            1 => L("Vm_SupportBundleOneFile"),
            _ => LF("Vm_SupportBundleManyFilesFormat", _supportBundlePreview.SelectedFileCount)
        };

    public string SupportBundlePreviewDetail =>
        LF("Vm_SupportBundleEstimatedSizeFormat", FormatBytes(_supportBundlePreview.EstimatedBytes));

    public string SupportBundleRedactionSummary =>
        L("Vm_SupportBundleRedactionSummary");

    public string SupportBundleLastExportText
    {
        get => _supportBundleLastExportText;
        private set => SetProperty(ref _supportBundleLastExportText, value);
    }

    public string RecommendedRunDuration =>
        Snapshot.SpotifyInstalled
            ? L("Vm_RecommendedDurationExistingSpotify")
            : L("Vm_RecommendedDurationCleanMachine");

    public string RecommendedFollowUpText =>
        HasConfigurationRecoveryNotice
            ? L("Vm_RecommendedFollowUpRecovery")
            : Snapshot.SavedConfigExists
            ? L("Vm_RecommendedFollowUpSavedProfile")
            : L("Vm_RecommendedFollowUpFirstProfile");

    public string CustomProfileTitle
    {
        get
        {
            var advancedCount = AdvancedOptions.Count(option => option.IsSelected);
            var selectedExtensions = Extensions.Count(item => item.IsSelected);
            var selectedCustomApps = CustomApps.Count(item => item.IsSelected);
            var selectedAddOns = selectedExtensions + selectedCustomApps;

            return advancedCount switch
            {
                0 when selectedAddOns <= 3 => L("Vm_CustomProfileNearDefault"),
                <= 2 => L("Vm_CustomProfileBalanced"),
                _ => L("Vm_CustomProfileHeavy")
            };
        }
    }

    public string CustomProfileDetail
    {
        get
        {
            var advancedCount = AdvancedOptions.Count(option => option.IsSelected);
            var selectedExtensions = Extensions.Count(item => item.IsSelected);
            var selectedCustomApps = CustomApps.Count(item => item.IsSelected);
            var selectedAddOns = selectedExtensions + selectedCustomApps;

            if (advancedCount == 0 && selectedAddOns <= 3)
            {
                return L("Vm_CustomProfileNearDefaultDetail");
            }

            if (advancedCount <= 2)
            {
                return L("Vm_CustomProfileBalancedDetail");
            }

            return L("Vm_CustomProfileHeavyDetail");
        }
    }

    public string CustomRunReadinessTitle
    {
        get
        {
            if (HasConfigurationRecoveryNotice)
            {
                return L("Vm_CustomReadinessFreshProfile");
            }

            if (HasConflictingSidebarOptions())
            {
                return L("Vm_CustomReadinessConflict");
            }

            if (CustomPatchesEnabled && !_customPatchValidation.IsValid)
            {
                return L("Vm_CustomReadinessPatchJson");
            }

            if (!IsOptionSelected(nameof(InstallConfiguration.CleanInstall)) && !Snapshot.SpotifyInstalled)
            {
                return L("Vm_CustomReadinessExistingInstall");
            }

            return Strings.SeverityReady;
        }
    }

    public string CustomRunReadinessDetail
    {
        get
        {
            if (HasConfigurationRecoveryNotice)
            {
                return L("Vm_CustomReadinessRecoveryDetail");
            }

            if (HasConflictingSidebarOptions())
            {
                return L("Vm_CustomReadinessConflictDetail");
            }

            if (CustomPatchesEnabled && !_customPatchValidation.IsValid)
            {
                return L("Vm_CustomReadinessPatchJsonDetail");
            }

            if (!IsOptionSelected(nameof(InstallConfiguration.CleanInstall)) && !Snapshot.SpotifyInstalled)
            {
                return L("Vm_CustomReadinessExistingInstallDetail");
            }

            return L("Vm_CustomReadinessReadyDetail");
        }
    }

    public string CustomApplyCaption =>
        HasConfigurationRecoveryNotice
            ? L("Vm_CustomApplyRecovery")
            : HasConflictingSidebarOptions()
                ? L("Vm_CustomApplyConflict")
            : CustomPatchesEnabled && !_customPatchValidation.IsValid
                ? L("Vm_CustomApplyPatchJson")
            : L("Vm_CustomApplyReady");

    public string WorkspaceHeroEyebrow => SelectedWorkspaceIndex switch
    {
        1 => L("Vm_WorkspaceHeroCustomEyebrow"),
        2 => L("Vm_WorkspaceHeroMaintenanceEyebrow"),
        _ => Strings.HeroGuidedSetup
    };

    public string WorkspaceHeroTitle => SelectedWorkspaceIndex switch
    {
        1 => L("Vm_WorkspaceHeroCustomTitle"),
        2 => L("Vm_WorkspaceHeroMaintenanceTitle"),
        _ => Strings.ModeRecommendedDescription
    };

    public string WorkspaceHeroBody => SelectedWorkspaceIndex switch
    {
        1 => L("Vm_WorkspaceHeroCustomBody"),
        2 => L("Vm_WorkspaceHeroMaintenanceBody"),
        _ => IsSnapshotLoading
            ? L("Vm_ShellCheckingSystemDetail")
            : HasSnapshotLoadError
                ? L("Vm_ShellSnapshotUnavailableDetail")
                : HasCriticalHealthIssues
                    ? HealthIssueSummary
                    : L("Vm_WorkspaceHeroRecommendedBody")
    };

    public int SelectedWorkspaceIndex
    {
        get => _selectedWorkspaceIndex;
        set
        {
            if (SetProperty(ref _selectedWorkspaceIndex, value))
            {
                OnPropertyChanged(nameof(WorkspaceHeroEyebrow));
                OnPropertyChanged(nameof(WorkspaceHeroTitle));
                OnPropertyChanged(nameof(WorkspaceHeroBody));
                OnPropertyChanged(nameof(ShowRecommendedRunBand));
            }
        }
    }

    public bool IsMaintenanceDiagnosticsExpanded
    {
        get => _isMaintenanceDiagnosticsExpanded;
        set => SetProperty(ref _isMaintenanceDiagnosticsExpanded, value);
    }

    public bool IsMaintenanceDangerExpanded
    {
        get => _isMaintenanceDangerExpanded;
        set => SetProperty(ref _isMaintenanceDangerExpanded, value);
    }

    public bool IsActivityVisible
    {
        get => _activityState.IsVisible;
        private set => _activityState.IsVisible = value;
    }

    public bool IsRunning
    {
        get => _activityState.IsRunning;
        private set => _activityState.IsRunning = value;
    }

    public bool IsCancelRequested
    {
        get => _activityState.IsCancelRequested;
        private set => _activityState.IsCancelRequested = value;
    }

    public double ProgressValue
    {
        get => _activityState.ProgressValue;
        private set => _activityState.ProgressValue = value;
    }

    public bool IsBusyIndeterminate => IsRunning && ProgressValue <= 0.0;

    // Mirror run state onto the taskbar icon so LibreSpot feels like a real
    // long-running Windows app even when the window is minimized.
    public System.Windows.Shell.TaskbarItemProgressState TaskbarProgressState
    {
        get
        {
            if (IsActivityError)
            {
                return System.Windows.Shell.TaskbarItemProgressState.Error;
            }
            if (IsCancelRequested)
            {
                return System.Windows.Shell.TaskbarItemProgressState.Paused;
            }
            if (!IsRunning)
            {
                return System.Windows.Shell.TaskbarItemProgressState.None;
            }
            return IsBusyIndeterminate
                ? System.Windows.Shell.TaskbarItemProgressState.Indeterminate
                : System.Windows.Shell.TaskbarItemProgressState.Normal;
        }
    }

    // TaskbarItemInfo.ProgressValue expects 0.0..1.0, but our ProgressValue is 0..100.
    public double TaskbarProgressFraction => Math.Clamp(ProgressValue / 100.0, 0.0, 1.0);

    // "â€” %" reads like a broken UI. When we don't yet have a real percentage
    // from the backend, say what is actually happening: we're working.
    public string ProgressLabel =>
        IsCancelRequested
            ? L("Vm_ProgressStopping")
            : IsBusyIndeterminate
            ? L("Vm_ProgressWorking")
            : IsRunning
                ? $"{Math.Round(ProgressValue)}%"
                : IsActivityCanceled ? Strings.Canceled
                : IsActivityError ? Strings.RunNeedsAttention
                : ProgressValue >= 100 ? L("Vm_ProgressDone") : Strings.SeverityReady;

    // Activity badge surfaces the run's outcome after completion so the overlay
    // isn't frozen on "Live run" once work is done. We derive from ActivityStatus
    // because HandleBackendMessage already reconciles status strings per outcome.
    public bool IsActivityError =>
        !IsRunning && _activityOutcome == ActivityOutcome.Error;

    public bool IsActivityCanceled =>
        !IsRunning && _activityOutcome == ActivityOutcome.Canceled;

    public bool CanExportFailureBundle =>
        !IsRunning && (IsActivityError || IsActivityCanceled);

    public string ActivityBadgeText =>
        IsCancelRequested ? L("Vm_ActivityBadgeStopping")
        : IsRunning ? Strings.StatusInProgress
        : IsActivityCanceled ? Strings.Canceled
        : IsActivityError ? Strings.StatusNeedsReview
        : ProgressValue >= 100 ? Strings.StatusComplete
        : Strings.SeverityReady;

    public string ActivityDetailLabel =>
        IsRunning || IsCancelRequested
            ? Strings.CurrentStep
            : Strings.RunStatus;

    public string ActivityTitle
    {
        get => _activityState.Title;
        private set => _activityState.Title = value;
    }

    public string ActivityStatus
    {
        get => _activityState.Status;
        private set => _activityState.Status = value;
    }

    public string ActivityStep
    {
        get => _activityState.Step;
        private set => _activityState.Step = value;
    }

    public string ActivityOperationId => _activityState.OperationId;
    public bool HasActivityOperationId => _activityState.HasOperationId;

    public string ActivityLiveAnnouncement =>
        string.Join(". ", new[] { ActivityStatus, ActivityStep }.Where(value => !string.IsNullOrWhiteSpace(value)));

    public string ActivityAssistiveText =>
        IsCancelRequested
            ? L("Vm_ActivityAssistiveStopping")
            : IsRunning
                ? L("Vm_ActivityAssistiveRunning")
                : IsActivityCanceled
                    ? L("Vm_ActivityAssistiveCanceled")
                : IsActivityError
                    ? L("Vm_ActivityAssistiveError")
                    : ProgressValue >= 100
                        ? L("Vm_ActivityAssistiveComplete")
                        : L("Vm_ActivityAssistiveIdle");

    public string ActivitySummaryTitle =>
        IsCancelRequested
            ? L("Vm_ActivitySummaryStopping")
            : IsRunning
                ? L("Vm_ActivitySummaryRunning")
                : IsActivityCanceled || IsActivityError
                    ? L("Vm_ActivitySummaryNextStepRecommended")
                    : ProgressValue >= 100
                        ? L("Vm_ActivitySummaryNextStep")
                        : L("Vm_ActivitySummarySessionDetails");

    public string ActivityLogPathText => LF("Vm_ActivityLogPathFormat", _configurationService.LogPath);

    public string RunElapsedText =>
        _runStopwatch.Elapsed.TotalHours >= 1
            ? _runStopwatch.Elapsed.ToString(@"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture)
            : _runStopwatch.Elapsed.ToString(@"mm\:ss", System.Globalization.CultureInfo.InvariantCulture);

    public string LogLineCountText => _activityState.LogLineCountText;

    public bool IsLogEmpty => _activityState.IsLogEmpty;

    public string LastRefreshedText => _environmentState.LastRefreshedText;

    public bool IsSnapshotStale => _environmentState.IsStale;

    public string SnapshotFreshnessTitle => _environmentState.FreshnessTitle;

    public string SnapshotFreshnessDetail => _environmentState.FreshnessDetail;

    public bool IsPromptVisible
    {
        get => _promptState.IsVisible;
    }

    public string PromptTitle
    {
        get => _promptState.Title;
    }

    public string PromptBody
    {
        get => _promptState.Body;
    }

    public string PromptConfirmText
    {
        get => _promptState.ConfirmText;
    }

    public string PromptCancelText
    {
        get => _promptState.CancelText;
    }

    public string PromptSummaryTitle
    {
        get => _promptState.SummaryTitle;
    }

    public string PromptSummaryBody
    {
        get => _promptState.SummaryBody;
    }

    public bool IsPromptDestructive
    {
        get => _promptState.IsDestructive;
    }

    public bool IsPromptConfirmDefault => _promptState.IsConfirmDefault;

    private void OnActivityStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ActivityRunStateViewModel.IsVisible):
                DismissActivityCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(IsActivityVisible));
                OnPropertyChanged(nameof(IsShellInteractionEnabled));
                break;
            case nameof(ActivityRunStateViewModel.IsRunning):
                OnPropertyChanged(nameof(IsRunning));
                RaiseRunCommandStateChanged();
                RaiseActivityDerivedStateChanged();
                break;
            case nameof(ActivityRunStateViewModel.IsCancelRequested):
                CancelRunCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(IsCancelRequested));
                RaiseActivityDerivedStateChanged();
                break;
            case nameof(ActivityRunStateViewModel.ProgressValue):
                OnPropertyChanged(nameof(ProgressValue));
                RaiseActivityDerivedStateChanged();
                break;
            case nameof(ActivityRunStateViewModel.Title):
                OnPropertyChanged(nameof(ActivityTitle));
                break;
            case nameof(ActivityRunStateViewModel.Status):
                OnPropertyChanged(nameof(ActivityStatus));
                OnPropertyChanged(nameof(ActivityLiveAnnouncement));
                RaiseActivityDerivedStateChanged();
                break;
            case nameof(ActivityRunStateViewModel.Step):
                OnPropertyChanged(nameof(ActivityStep));
                OnPropertyChanged(nameof(ActivityLiveAnnouncement));
                break;
            case nameof(ActivityRunStateViewModel.OperationId):
                OnPropertyChanged(nameof(ActivityOperationId));
                OnPropertyChanged(nameof(HasActivityOperationId));
                CopyOperationIdCommand.NotifyCanExecuteChanged();
                break;
            case nameof(ActivityRunStateViewModel.LogLineCountText):
                CopyLogCommand.NotifyCanExecuteChanged();
                ClearLogCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(LogLineCountText));
                OnPropertyChanged(nameof(ShellSummaryItems));
                OnPropertyChanged(nameof(ShellActivityLogItems));
                OnPropertyChanged(nameof(HasShellActivityLogItems));
                OnPropertyChanged(nameof(ShowShellActivityEmptyState));
                OnPropertyChanged(nameof(ShellActivityEmptyTitle));
                OnPropertyChanged(nameof(ShellActivityEmptyDetail));
                break;
            case nameof(ActivityRunStateViewModel.IsLogEmpty):
                OnPropertyChanged(nameof(IsLogEmpty));
                OnPropertyChanged(nameof(ShellActivityLogItems));
                OnPropertyChanged(nameof(HasShellActivityLogItems));
                OnPropertyChanged(nameof(ShowShellActivityEmptyState));
                OnPropertyChanged(nameof(ShellActivityEmptyTitle));
                OnPropertyChanged(nameof(ShellActivityEmptyDetail));
                break;
            case nameof(ActivityRunStateViewModel.HasUndoActionItems):
                OnPropertyChanged(nameof(HasUndoActionItems));
                break;
            case nameof(ActivityRunStateViewModel.HasExecutableUndoActionItems):
                OnPropertyChanged(nameof(HasExecutableUndoActionItems));
                PreviewSelectedUndoCommand.NotifyCanExecuteChanged();
                ExecuteSelectedUndoCommand.NotifyCanExecuteChanged();
                break;
        }
    }

    private void RaiseRunCommandStateChanged()
    {
        ApplyRecommendedCommand.NotifyCanExecuteChanged();
        ApplyCustomCommand.NotifyCanExecuteChanged();
        OpenSpotifyCommand.NotifyCanExecuteChanged();
        CancelRunCommand.NotifyCanExecuteChanged();
        DismissActivityCommand.NotifyCanExecuteChanged();
        EnableAutoReapplyCommand.NotifyCanExecuteChanged();
        DisableAutoReapplyCommand.NotifyCanExecuteChanged();
        ExportSupportBundleCommand.NotifyCanExecuteChanged();
        ExportFailureBundleCommand.NotifyCanExecuteChanged();
        ClearAssetCacheCommand.NotifyCanExecuteChanged();
        ConfirmPromptCommand.NotifyCanExecuteChanged();
        CancelPromptCommand.NotifyCanExecuteChanged();
        ValidateCustomPatchesCommand.NotifyCanExecuteChanged();
        FormatCustomPatchesCommand.NotifyCanExecuteChanged();
        ClearCustomPatchesCommand.NotifyCanExecuteChanged();
        ImportCustomPatchesFromUrlCommand.NotifyCanExecuteChanged();
        PreviewSelectedUndoCommand.NotifyCanExecuteChanged();
        ExecuteSelectedUndoCommand.NotifyCanExecuteChanged();
        RaiseLocalProfileCommandStateChanged();
        OnPropertyChanged(nameof(ProfileSelectionHint));
        RaiseMaintenanceActionCanExecuteChanged();
        RaiseShellChromeChanged();
    }

    private void RaiseActivityDerivedStateChanged()
    {
        OnPropertyChanged(nameof(IsBusyIndeterminate));
        OnPropertyChanged(nameof(ProgressLabel));
        OnPropertyChanged(nameof(IsActivityError));
        OnPropertyChanged(nameof(IsActivityCanceled));
        OnPropertyChanged(nameof(CanExportFailureBundle));
        OnPropertyChanged(nameof(ActivityBadgeText));
        OnPropertyChanged(nameof(ActivityDetailLabel));
        OnPropertyChanged(nameof(ActivityAssistiveText));
        OnPropertyChanged(nameof(ActivitySummaryTitle));
        OnPropertyChanged(nameof(TaskbarProgressState));
        OnPropertyChanged(nameof(TaskbarProgressFraction));
        ExportFailureBundleCommand.NotifyCanExecuteChanged();
        RaiseShellChromeChanged();
    }

    private void RaiseShellChromeChanged()
    {
        OnPropertyChanged(nameof(ShellReadinessValue));
        OnPropertyChanged(nameof(ShellReadinessDetail));
        OnPropertyChanged(nameof(ShellStackStatusTitle));
        OnPropertyChanged(nameof(ShellStackStatusDetail));
        OnPropertyChanged(nameof(ShellSummaryItems));
        OnPropertyChanged(nameof(ShellEnvironmentRows));
        OnPropertyChanged(nameof(ShellDependencyRows));
        OnPropertyChanged(nameof(ShellDependenciesSummaryText));
        OnPropertyChanged(nameof(ShellActivityLogItems));
        OnPropertyChanged(nameof(HasShellActivityLogItems));
        OnPropertyChanged(nameof(ShowShellActivityEmptyState));
        OnPropertyChanged(nameof(ShellBackupCreatedDetail));
        OnPropertyChanged(nameof(ShellNoActiveTasksText));
        OnPropertyChanged(nameof(ShellServiceStatusText));
        OnPropertyChanged(nameof(ShellReadinessChecks));
        OnPropertyChanged(nameof(SimpleHomeReadinessChecks));
        OnPropertyChanged(nameof(ShellReadinessPercent));
        OnPropertyChanged(nameof(HomeAction));
        OnPropertyChanged(nameof(MaintenanceRecommendation));
        OnPropertyChanged(nameof(MaintenanceOverallStatus));
        OnPropertyChanged(nameof(MaintenanceOverallTone));
        OnPropertyChanged(nameof(MaintenanceGuidanceTitle));
        OnPropertyChanged(nameof(MaintenanceGuidanceDetail));
        OnPropertyChanged(nameof(WorkspaceHeroBody));
    }

    private void OnCustomOptionEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CustomOptionEditorStateViewModel.SelectedTheme):
                OnPropertyChanged(nameof(SelectedTheme));
                OnPropertyChanged(nameof(SelectedThemeGalleryItem));
                RaiseSelectionInsightsChanged();
                break;
            case nameof(CustomOptionEditorStateViewModel.SelectedThemeGalleryItem):
                OnPropertyChanged(nameof(SelectedThemeGalleryItem));
                break;
            case nameof(CustomOptionEditorStateViewModel.ThemeSearchText):
                OnPropertyChanged(nameof(ThemeSearchText));
                ClearThemeSearchCommand.NotifyCanExecuteChanged();
                break;
            case nameof(CustomOptionEditorStateViewModel.FilteredThemeGalleryItems):
                OnPropertyChanged(nameof(FilteredThemeGalleryItems));
                break;
            case nameof(CustomOptionEditorStateViewModel.ThemeGalleryEmptyText):
                OnPropertyChanged(nameof(ThemeGalleryEmptyText));
                break;
            case nameof(CustomOptionEditorStateViewModel.ShowThemeGalleryEmptyState):
                OnPropertyChanged(nameof(ShowThemeGalleryEmptyState));
                break;
            case nameof(CustomOptionEditorStateViewModel.HasThemeSearchText):
                OnPropertyChanged(nameof(HasThemeSearchText));
                break;
            case nameof(CustomOptionEditorStateViewModel.SelectedScheme):
                OnPropertyChanged(nameof(SelectedScheme));
                RaiseSelectionInsightsChanged();
                break;
            case nameof(CustomOptionEditorStateViewModel.SelectedLyricsTheme):
                OnPropertyChanged(nameof(SelectedLyricsTheme));
                RaiseSelectionInsightsChanged();
                break;
            case nameof(CustomOptionEditorStateViewModel.SelectedSpotifyVersionId):
                OnPropertyChanged(nameof(SelectedSpotifyVersionId));
                RaiseSelectionInsightsChanged();
                break;
            case nameof(CustomOptionEditorStateViewModel.SelectedDownloadMethod):
                OnPropertyChanged(nameof(SelectedDownloadMethod));
                RaiseSelectionInsightsChanged();
                break;
            case nameof(CustomOptionEditorStateViewModel.CacheLimitText):
                OnPropertyChanged(nameof(CacheLimitText));
                RaiseSelectionInsightsChanged();
                break;
            case nameof(CustomOptionEditorStateViewModel.FeatureSearchText):
                OnPropertyChanged(nameof(FeatureSearchText));
                RaiseLiveCustomizationFilterChanged();
                break;
            case nameof(CustomOptionEditorStateViewModel.SelectedFeatureGroup):
                OnPropertyChanged(nameof(SelectedFeatureGroup));
                RaiseLiveCustomizationFilterChanged();
                break;
        }
    }

    private void OnEnvironmentStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(EnvironmentSnapshotStateViewModel.Snapshot):
                OnPropertyChanged(nameof(Snapshot));
                RaiseAutoReapplyStateChanged();
                RefreshSupportBundlePreview();
                break;
            case nameof(EnvironmentSnapshotStateViewModel.LastRefreshedText):
                OnPropertyChanged(nameof(LastRefreshedText));
                break;
            case nameof(EnvironmentSnapshotStateViewModel.IsStale):
                OnPropertyChanged(nameof(IsSnapshotStale));
                break;
            case nameof(EnvironmentSnapshotStateViewModel.FreshnessTitle):
                OnPropertyChanged(nameof(SnapshotFreshnessTitle));
                break;
            case nameof(EnvironmentSnapshotStateViewModel.FreshnessDetail):
                OnPropertyChanged(nameof(SnapshotFreshnessDetail));
                break;
        }
    }

    private void OnPromptStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PromptStateViewModel.IsVisible):
                ConfirmPromptCommand.NotifyCanExecuteChanged();
                CancelPromptCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(IsPromptVisible));
                OnPropertyChanged(nameof(IsPromptConfirmDefault));
                OnPropertyChanged(nameof(IsShellInteractionEnabled));
                break;
            case nameof(PromptStateViewModel.Title):
                OnPropertyChanged(nameof(PromptTitle));
                break;
            case nameof(PromptStateViewModel.Body):
                OnPropertyChanged(nameof(PromptBody));
                break;
            case nameof(PromptStateViewModel.ConfirmText):
                OnPropertyChanged(nameof(PromptConfirmText));
                break;
            case nameof(PromptStateViewModel.CancelText):
                OnPropertyChanged(nameof(PromptCancelText));
                break;
            case nameof(PromptStateViewModel.SummaryTitle):
                OnPropertyChanged(nameof(PromptSummaryTitle));
                break;
            case nameof(PromptStateViewModel.SummaryBody):
                OnPropertyChanged(nameof(PromptSummaryBody));
                break;
            case nameof(PromptStateViewModel.IsDestructive):
                OnPropertyChanged(nameof(IsPromptDestructive));
                OnPropertyChanged(nameof(IsPromptConfirmDefault));
                break;
            case nameof(PromptStateViewModel.IsConfirmDefault):
                OnPropertyChanged(nameof(IsPromptConfirmDefault));
                break;
        }
    }

    private void OnSettingsSearchStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsSearchStateViewModel.Text))
        {
            OnPropertyChanged(nameof(SettingsSearchText));
            RefreshSettingsSearch();
        }
    }

    private void OnLocalizationCultureChanged(object? sender, EventArgs e) =>
        _dispatcher.BeginInvoke(
            new Action(() =>
            {
                RaiseLocalizedTextChanged();
                if (!_applyingCultureFromConfig)
                {
                    _ = RefreshSnapshotAsync();
                }
            }),
            DispatcherPriority.Background);

    private void ApplyCultureFromConfiguration(string? cultureName)
    {
        var normalized = LocalizationService.NormalizeCultureName(cultureName);
        var option = LocalizationOptions.First(item =>
            string.Equals(item.CultureName, normalized, StringComparison.OrdinalIgnoreCase));

        _applyingCultureFromConfig = true;
        try
        {
            SelectedLocalizationOption = option;
            _localizationService.ApplyCulture(option.CultureName);
        }
        finally
        {
            _applyingCultureFromConfig = false;
        }
    }

    private async Task PersistUiCultureAsync(string cultureName)
    {
        try
        {
            var configuration = await _configurationService.LoadAsync();
            configuration.UiCulture = LocalizationService.NormalizeCultureName(cultureName);
            await _configurationService.SaveAsync(configuration);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to persist UI culture preference");
        }
    }

    private void RaiseLocalizedTextChanged()
    {
        RaiseLibreSpotUpdateNoticeChanged();
        RefreshSettingsSearch();
        _customOptions.RefreshLocalizedText();
        _maintenanceActions.RefreshLocalizedText();
        _environmentState.RefreshFreshness();
        foreach (var profile in LocalProfiles)
        {
            profile.RefreshLocalizedText();
        }

        RebuildSelectionInsights();
        RaiseSnapshotInsightsChanged();
        RaiseLocalProfileStateChanged();
        _activityState.RefreshLocalizedText();
        if (SelectedLocalProfile is null)
        {
            SelectedProfileShareStatus = L("Vm_ProfileShareInitial");
            SelectedProfileComparisonText = L("Vm_ProfileComparisonInitial");
        }
        else
        {
            _selectedProfileShareRefreshTask = RefreshSelectedProfileShareCardAsync();
        }

        RaiseActivityDerivedStateChanged();
        RaiseSupportBundlePreviewChanged();
        OnPropertyChanged(nameof(SelectedLocalizationOption));
        OnPropertyChanged(nameof(ShellReadinessTitle));
        OnPropertyChanged(nameof(ShellReadinessShortLabel));
        OnPropertyChanged(nameof(ShellQuickActionsTitle));
        OnPropertyChanged(nameof(ShellNextActionsTitle));
        OnPropertyChanged(nameof(ShellActionRunSetupTitle));
        OnPropertyChanged(nameof(ShellActionRunSetupDetail));
        OnPropertyChanged(nameof(ShellActionUnblockTitle));
        OnPropertyChanged(nameof(ShellActionUnblockDetail));
        OnPropertyChanged(nameof(ShellActionToolsTitle));
        OnPropertyChanged(nameof(ShellActionToolsDetail));
        OnPropertyChanged(nameof(ShellSystemChecksLabel));
        OnPropertyChanged(nameof(ShellSpotifyDetectedLabel));
        OnPropertyChanged(nameof(ShellWritePermissionsLabel));
        OnPropertyChanged(nameof(ShellDependenciesLabel));
        OnPropertyChanged(nameof(ShellCheckOkLabel));
        OnPropertyChanged(nameof(ShellReadinessChecks));
        OnPropertyChanged(nameof(SimpleHomeReadinessChecks));
        OnPropertyChanged(nameof(ShellVerifyEnvironmentTitle));
        OnPropertyChanged(nameof(ShellVerifyEnvironmentDetail));
        OnPropertyChanged(nameof(ShellRepairTitle));
        OnPropertyChanged(nameof(ShellRepairDetail));
        OnPropertyChanged(nameof(ShellClearCacheTitle));
        OnPropertyChanged(nameof(ShellClearCacheDetail));
        OnPropertyChanged(nameof(ShellTrustRiskTitle));
        OnPropertyChanged(nameof(ShellTrustedSourcesTitle));
        OnPropertyChanged(nameof(ShellTrustedSourcesDetail));
        OnPropertyChanged(nameof(ShellProvenanceTitle));
        OnPropertyChanged(nameof(ShellProvenanceDetail));
        OnPropertyChanged(nameof(ShellProvenanceItems));
        OnPropertyChanged(nameof(ShellSpotifyModificationTitle));
        OnPropertyChanged(nameof(ShellSpotifyModificationDetail));
        OnPropertyChanged(nameof(ShellBackupCreatedTitle));
        OnPropertyChanged(nameof(ShellActivityTitle));
        OnPropertyChanged(nameof(ShellReadyText));
        OnPropertyChanged(nameof(ShellTopThemeLabel));
        OnPropertyChanged(nameof(ShellTopSettingsLabel));
        OnPropertyChanged(nameof(ShellLearnMoreLabel));
        OnPropertyChanged(nameof(ShellLogLevelLabel));
        OnPropertyChanged(nameof(ShellClearLogLabel));
        OnPropertyChanged(nameof(ShellClearLogHint));
        OnPropertyChanged(nameof(ShellLogFilterHint));
        OnPropertyChanged(nameof(ShellActivityEmptyTitle));
        OnPropertyChanged(nameof(ShellActivityEmptyDetail));
        OnPropertyChanged(nameof(ShellAutoScrollLabel));
        OnPropertyChanged(nameof(ShellRunRecommendedCaption));
        OnPropertyChanged(nameof(ShellActiveRunTitle));
        OnPropertyChanged(nameof(ShellLocalEnvironmentTitle));
        OnPropertyChanged(nameof(ShellDependenciesTitle));
        OnPropertyChanged(nameof(ShellDependencyComponentHeader));
        OnPropertyChanged(nameof(ShellDependencyInstalledHeader));
        OnPropertyChanged(nameof(ShellDependencyRecommendedHeader));
        OnPropertyChanged(nameof(ShellDependencyStatusHeader));
        OnPropertyChanged(nameof(ShellEnvironmentReportLinkText));
        OnPropertyChanged(nameof(StatusDashboardItems));
        OnPropertyChanged(nameof(ShellPrimaryStatusItems));
        OnPropertyChanged(nameof(CustomAppsSectionTitle));
        OnPropertyChanged(nameof(CustomAppsSectionDescription));
        OnPropertyChanged(nameof(CustomPatchesImportProvenance));
        OnPropertyChanged(nameof(SupportBundleLastExportText));
        OnPropertyChanged(nameof(ActivityLogPathText));
        OnPropertyChanged(nameof(WorkspaceHeroEyebrow));
        OnPropertyChanged(nameof(WorkspaceHeroTitle));
        OnPropertyChanged(nameof(WorkspaceHeroBody));
    }

    public async Task InitializeAsync()
    {
        var loadResult = await _configurationService.LoadResultAsync();
        _configurationLoadState = loadResult.State;
        _recoveredConfigurationPath = loadResult.RecoveredFilePath;
        _configurationRecoveryReason = loadResult.RecoveryReason;
        ApplyCultureFromConfiguration(loadResult.Configuration.UiCulture);
        ApplyConfigurationToEditor(loadResult.Configuration);
        _libreSpotUpdateCheck = CheckForLibreSpotUpdateAsync();
        await RefreshLocalProfilesAsync();
        await RefreshSnapshotAsync();
    }

    private async Task CheckForLibreSpotUpdateAsync()
    {
        if (_releaseNoticeProbe is null)
        {
            return;
        }

        try
        {
            var notice = await _releaseNoticeProbe(ProductVersion, _releaseNoticeCts.Token);
            if (_releaseNoticeCts.IsCancellationRequested)
            {
                return;
            }

            _libreSpotUpdateNotice = notice;
            RaiseLibreSpotUpdateNoticeChanged();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "LibreSpot release notice check failed");
        }
    }

    private void RaiseLibreSpotUpdateNoticeChanged()
    {
        OnPropertyChanged(nameof(HasLibreSpotUpdateNotice));
        OnPropertyChanged(nameof(LibreSpotUpdateNoticeText));
        OnPropertyChanged(nameof(LibreSpotUpdateNoticeLinkLabel));
        OnPropertyChanged(nameof(LibreSpotUpdateNoticeAutomationName));
        (OpenLibreSpotUpdateCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void OpenLibreSpotUpdate()
    {
        if (_libreSpotUpdateNotice is { UpdateAvailable: true })
        {
            // The link only ever opens the GitHub release page; anything else
            // (including a tampered cache) falls back to the releases page.
            OpenExternalUri(ReleaseNoticeService.IsTrustedReleaseUrl(_libreSpotUpdateNotice.ReleaseUrl)
                ? _libreSpotUpdateNotice.ReleaseUrl!
                : ReleaseNoticeService.LatestStableReleasePage);
        }
    }

    public void ApplyInitializationFailure()
    {
        Interlocked.Increment(ref _snapshotRequestVersion);
        SetSnapshotQueryState(isLoading: false, loadFailed: true);
        AppendLog(L("Vm_ShellSnapshotUnavailableDetail"), "ERROR");
    }


    private void RaiseSnapshotInsightsChanged()
    {
        RefreshMaintenanceActionRelevance();
        RaiseShellChromeChanged();
        OnPropertyChanged(nameof(SpotifyStatusLine));
        OnPropertyChanged(nameof(CustomizationStatusLine));
        OnPropertyChanged(nameof(MarketplaceStatusLine));
        OnPropertyChanged(nameof(HealthReport));
        OnPropertyChanged(nameof(CriticalHealthIssues));
        OnPropertyChanged(nameof(WarningHealthIssues));
        OnPropertyChanged(nameof(InfoHealthIssues));
        OnPropertyChanged(nameof(HasCriticalHealthIssues));
        OnPropertyChanged(nameof(HealthIssueSummary));
        OnPropertyChanged(nameof(StatusDashboardItems));
        OnPropertyChanged(nameof(ShellPrimaryStatusItems));
        OnPropertyChanged(nameof(CompatibilityVerdictItems));
        OnPropertyChanged(nameof(CompatibilityVerdictSummary));
        OnPropertyChanged(nameof(MaintenanceReadinessValue));
        OnPropertyChanged(nameof(MaintenanceReadinessDetail));
        OnPropertyChanged(nameof(MaintenanceBackupValue));
        OnPropertyChanged(nameof(MaintenanceBackupDetail));
        OnPropertyChanged(nameof(MaintenanceMarketplaceValue));
        OnPropertyChanged(nameof(MaintenanceMarketplaceDetail));
        OnPropertyChanged(nameof(MaintenanceThemeValue));
        OnPropertyChanged(nameof(MaintenanceThemeDetail));
        OnPropertyChanged(nameof(ShellProvenanceItems));
        RaiseSupportBundlePreviewChanged();
        OnPropertyChanged(nameof(HasConfigurationRecoveryNotice));
        OnPropertyChanged(nameof(ConfigurationRecoveryTitle));
        OnPropertyChanged(nameof(ConfigurationRecoveryDetail));
        OnPropertyChanged(nameof(ProfileStatusLine));
        OnPropertyChanged(nameof(WorkspaceRecommendationTitle));
        OnPropertyChanged(nameof(WorkspaceRecommendationBrief));
        OnPropertyChanged(nameof(MaintenanceGuidanceTitle));
        OnPropertyChanged(nameof(MaintenanceGuidanceDetail));
        RaiseAutoReapplyStateChanged();
        OnPropertyChanged(nameof(AccessPostureLabel));
        OnPropertyChanged(nameof(RecommendedRunDuration));
        OnPropertyChanged(nameof(RecommendedFollowUpText));
        OnPropertyChanged(nameof(CustomRunReadinessTitle));
        OnPropertyChanged(nameof(CustomRunReadinessDetail));
        OnPropertyChanged(nameof(CustomApplyCaption));
        RefreshGlobalSearch();
        RebuildSelectionInsights();
    }





    private async Task StartBackendRunAsync(
        string action,
        InstallConfiguration? configuration,
        string title,
        string status,
        int targetWorkspaceIndex)
    {
        // Critical: flip IsRunning synchronously *before* any await so the
        // Apply button's CanExecute immediately returns false. Without this
        // a rapid double-click queues two concurrent backend runs.
        if (IsRunning)
        {
            return;
        }

        SelectedWorkspaceIndex = targetWorkspaceIndex;
        ClearUndoActionItems();
        ClearLog();
        _activityOutcome = ActivityOutcome.None;
        _lastBackendAction = action;
        _lastBackendRunResult = null;
        _lastRunStartedAt = DateTimeOffset.Now;
        _lastRunCompletedAt = null;
        var operationId = OperationCorrelation.Begin("WPF", action);
        _activityState.Begin(title, status, Strings.PreparingBackend, operationId.ToString());
        AppendLog(LF("Vm_OperationStartedFormat", operationId), "INFO");
        _runStopwatch.Restart();
        _runElapsedTimer.Start();
        OnPropertyChanged(nameof(RunElapsedText));

        _runCts?.Dispose();
        _runCts = new CancellationTokenSource();
        var token = _runCts.Token;
        var runSucceeded = false;

        try
        {
            if (configuration is not null)
            {
                try
                {
                    await _configurationService.SaveAsync(configuration, token);
                    _configurationLoadState = ConfigurationLoadState.Loaded;
                    _recoveredConfigurationPath = null;
                }
                catch (OperationCanceledException)
                {
                    AppendLog(L("Vm_LogConfigSaveCanceled"), "WARN");
                    _activityOutcome = ActivityOutcome.Canceled;
                    ActivityStatus = Strings.Canceled;
                    ActivityStep = Strings.ConfigSaveCanceled;
                    return;
                }
                catch (Exception ex)
                {
                    AppendLog(LF("Vm_LogConfigSaveFailed", ex.Message), "ERROR");
                    _activityOutcome = ActivityOutcome.Error;
                    ActivityStatus = Strings.RunNeedsAttention;
                    ActivityStep = L("Vm_ConfigSaveFailed");
                    ProgressValue = 100;
                    return;
                }

                ApplyConfigurationToEditor(configuration);
            }

            var result = await _backendScriptService.RunAsync(
                action,
                _configurationService.ConfigPath,
                HandleBackendMessage,
                operationId,
                token);
            _lastBackendRunResult = result;
            if (result.Canceled)
            {
                AppendLog(result.ErrorMessage ?? L("Vm_LogBackendCanceled"), "WARN");
                _activityOutcome = ActivityOutcome.Canceled;
                ActivityStatus = Strings.Canceled;
            }
            else if (!result.Success)
            {
                AppendLog(result.ErrorMessage ?? L("Vm_UnknownBackendFailure"), "ERROR");
                _activityOutcome = ActivityOutcome.Error;
                ActivityStatus = Strings.RunNeedsAttention;
            }
            else
            {
                runSucceeded = true;
                await RestartSpotifyAfterSuccessfulRunAsync(action, configuration, token);
            }
        }
        catch (OperationCanceledException)
        {
            _lastBackendRunResult = new BackendRunResult(false, L("Vm_LogBackendCanceled"), Canceled: true, ErrorCode: "DesktopCancellation");
            AppendLog(L("Vm_LogBackendCanceled"), "WARN");
            _activityOutcome = ActivityOutcome.Canceled;
            ActivityStatus = Strings.Canceled;
        }
        catch (Exception ex)
        {
            _lastBackendRunResult = new BackendRunResult(false, ex.Message, ErrorCode: "DesktopException");
            AppendLog(LF("Vm_LogBackendRunFailed", ex.Message), "ERROR");
            _activityOutcome = ActivityOutcome.Error;
            ActivityStatus = Strings.RunNeedsAttention;
        }
        finally
        {
            _lastRunCompletedAt = DateTimeOffset.Now;
            _runStopwatch.Stop();
            _runElapsedTimer.Stop();
            OnPropertyChanged(nameof(RunElapsedText));
            IsRunning = false;
            IsCancelRequested = false;
            OperationCorrelation.Complete(operationId, "WPF", action, _activityOutcome.ToString());
            await RefreshSnapshotAsync();
            if (runSucceeded)
            {
                RefreshUndoActionItems();
            }
        }

        // A run that could not install something the user selected has to be
        // read before the window goes away, so the auto-exit does not apply to it.
        if (ExitAfterSuccessfulSetup
            && runSucceeded
            && _activityOutcome != ActivityOutcome.Warning
            && ShouldExitAfterSuccessfulRun(action, configuration))
        {
            ScheduleApplicationExit();
        }
    }

    /// <summary>
    /// When true, the shell closes itself after a completed setup/change run.
    /// Off by default so unit tests and the UI-automation smoke view model never
    /// trigger a shutdown; only the real runtime window opts in.
    /// </summary>
    public bool ExitAfterSuccessfulSetup { get; set; }

    // A completed setup/change operation (the same set that restarts Spotify)
    // leaves the user done with LibreSpot, so the shell closes itself. Read-only
    // or continue-working actions (Check Updates, backups, watcher toggles) keep
    // the window open.
    private static bool ShouldExitAfterSuccessfulRun(string action, InstallConfiguration? configuration) =>
        action != "SafeMode" && ShouldRestartSpotifyAfterSuccessfulRun(action, configuration);

    private void ScheduleApplicationExit()
    {
        if (Application.Current is null)
        {
            // Headless (no WPF Application) — nothing to close.
            return;
        }

        AppendLog(L("Vm_SetupCompleteClosingLog"), "INFO");
        ActivityStep = L("Vm_ClosingLibreSpot");

        // Let the completion state render and the reopened Spotify settle for a
        // moment, then shut the shell down on the UI thread.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try { Application.Current?.Shutdown(); } catch { }
        };
        timer.Start();
    }

    private async Task OpenSpotifyAsync()
    {
        var result = await _spotifyProcessService.OpenAsync(
            HealthComponent("spotify")?.Path,
            CancellationToken.None);

        AppendLog(result.Message, result.Opened ? "INFO" : "WARN");
        if (!result.Opened)
        {
            ShowNotice(
                Strings.RunNeedsAttention,
                result.Message,
                L("Vm_SpotifyRestartSkipped"));
        }
    }

    private async Task RestartSpotifyAfterSuccessfulRunAsync(
        string action,
        InstallConfiguration? configuration,
        CancellationToken cancellationToken)
    {
        if (!ShouldRestartSpotifyAfterSuccessfulRun(action, configuration))
        {
            return;
        }

        ActivityStatus = L("Vm_RestartingSpotify");
        ActivityStep = L("Vm_ClosingSpotifyProcess");
        AppendLog(L("Vm_RestartingSpotifyLog"), "INFO");

        var result = await _spotifyProcessService.RestartAsync(
            HealthComponent("spotify")?.Path,
            TimeSpan.FromSeconds(3),
            cancellationToken);

        foreach (var shutdownEvent in result.ShutdownEvents ?? [])
        {
            AppendLog(shutdownEvent.Message, shutdownEvent.Level);
        }

        AppendLog(result.Message, result.Reopened ? "INFO" : "WARN");

        // Restarting Spotify is the last thing a successful run does, and it used
        // to declare the run complete on its way out. That overwrote the warning
        // naming an asset the user picked and did not get, which arrives earlier
        // on the result line. Spotify still restarts; the verdict is not reset.
        if (_activityOutcome != ActivityOutcome.Warning)
        {
            ActivityStatus = Strings.RunComplete;
            ActivityStep = result.Reopened ? L("Vm_SpotifyReopened") : L("Vm_SpotifyRestartSkipped");
        }

        ProgressValue = 100;
    }

    private static bool ShouldRestartSpotifyAfterSuccessfulRun(string action, InstallConfiguration? configuration) =>
        action switch
        {
            "Install" => configuration?.LaunchAfter ?? true,
            "Reapply" or "RepairMarketplace" or "SafeMode" or "RestoreSafeMode" or "RestoreBackup" or "RestoreVanilla" => true,
            _ => false
        };

    /// <summary>
    /// Requests cancellation of an in-flight backend run. Safe to call during window
    /// shutdown â€” if no run is active or the CTS has already been disposed this is a no-op.
    /// </summary>
    public void CancelRunningBackend()
    {
        if (IsRunning && !IsCancelRequested)
        {
            IsCancelRequested = true;
            ActivityStatus = Strings.StoppingBackend;
            ActivityStep = L("Vm_CancelRequested");
        }

        // ObjectDisposedException is possible if Dispose() already ran; treat the same
        // as "nothing to cancel." Any other exception here would indicate a programming
        // bug worth surfacing, so we don't blanket-catch.
        try { _runCts?.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    public void Dispose()
    {
        try { _runCts?.Cancel(); }
        catch (ObjectDisposedException) { }
        catch { }
        try { _releaseNoticeCts.Cancel(); _releaseNoticeCts.Dispose(); }
        catch { }
        _runElapsedTimer.Stop();
        _snapshotFreshnessTimer.Stop();
        _activityState.PropertyChanged -= OnActivityStatePropertyChanged;
        _customOptions.PropertyChanged -= OnCustomOptionEditorPropertyChanged;
        _environmentState.PropertyChanged -= OnEnvironmentStatePropertyChanged;
        _promptState.PropertyChanged -= OnPromptStatePropertyChanged;
        _settingsSearch.PropertyChanged -= OnSettingsSearchStatePropertyChanged;
        _localizationService.CultureChanged -= OnLocalizationCultureChanged;
        _runCts?.Dispose();
        _runCts = null;
    }

    private void CycleShellLogFilter()
    {
        _shellLogFilterIndex = (_shellLogFilterIndex + 1) % 3;
        OnPropertyChanged(nameof(ShellLogLevelLabel));
        OnPropertyChanged(nameof(ShellActivityLogItems));
        OnPropertyChanged(nameof(HasShellActivityLogItems));
        OnPropertyChanged(nameof(ShowShellActivityEmptyState));
        OnPropertyChanged(nameof(ShellActivityEmptyTitle));
        OnPropertyChanged(nameof(ShellActivityEmptyDetail));
    }

    private bool IsShellLogEntryVisible(LogEntryViewModel entry) => _shellLogFilterIndex switch
    {
        1 => string.Equals(entry.Level, "WARN", StringComparison.OrdinalIgnoreCase)
             || string.Equals(entry.Level, "ERROR", StringComparison.OrdinalIgnoreCase),
        2 => string.Equals(entry.Level, "ERROR", StringComparison.OrdinalIgnoreCase),
        _ => true
    };

    private void ClearUndoActionItems() => _activityState.ClearUndoActionItems();

    private void RefreshUndoActionItems()
    {
        try
        {
            _activityState.ReplaceUndoActionItems(_operationJournalUndoService.ReadLatestUndoItems(_configurationService.ConfigDirectory));
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Operation journal undo pane refresh failed");
        }
    }

    private IReadOnlyList<UndoActionItemViewModel> SelectedExecutableUndoItems() =>
        UndoActionItems.Where(item => item.IsExecutable && item.IsSelected).ToArray();

    private void PreviewSelectedUndo()
    {
        var selected = SelectedExecutableUndoItems();
        if (selected.Count == 0)
        {
            ShowNotice(L("Vm_UndoPreviewTitle"), L("Vm_UndoNoneSelected"), L("Vm_UndoSelectLowRisk"));
            return;
        }

        var previews = selected
            .Select(item => _operationJournalUndoService.PreviewUndoItem(item.Item, _configurationService.ConfigDirectory))
            .ToArray();
        var accepted = previews.Count(preview => preview.CanExecute);
        ShowNotice(
            L("Vm_UndoPreviewTitle"),
            LF("Vm_UndoPreviewSummaryFormat", accepted, previews.Length),
            string.Join(Environment.NewLine, previews.Select(preview => $"• {preview.Item.TokenKind}: {preview.Reason}")));
    }

    private void PresentSelectedUndoConfirmation()
    {
        var selected = SelectedExecutableUndoItems();
        if (selected.Count == 0)
        {
            ShowNotice(L("Vm_UndoPreviewTitle"), L("Vm_UndoNoneSelected"), L("Vm_UndoSelectLowRisk"));
            return;
        }

        var previews = selected
            .Select(item => _operationJournalUndoService.PreviewUndoItem(item.Item, _configurationService.ConfigDirectory))
            .ToArray();
        if (previews.Any(preview => !preview.CanExecute))
        {
            ShowNotice(
                L("Vm_UndoPreviewTitle"),
                L("Vm_UndoStateChanged"),
                string.Join(Environment.NewLine, previews.Where(preview => !preview.CanExecute).Select(preview => preview.Reason)));
            return;
        }

        ShowPrompt(
            L("Vm_UndoConfirmTitle"),
            LF("Vm_UndoConfirmBodyFormat", selected.Count),
            L("Vm_UndoConfirmButton"),
            Strings.ButtonCancel,
            false,
            () => ExecuteSelectedUndoConfirmedAsync(selected),
            L("Vm_PromptWhatThisWillDo"),
            string.Join(Environment.NewLine, previews.Select(preview => preview.Reason)));
    }

    private async Task ExecuteSelectedUndoConfirmedAsync(IReadOnlyList<UndoActionItemViewModel> selected)
    {
        var operationId = OperationCorrelation.Begin("WPF", "Undo");
        _activityOutcome = ActivityOutcome.None;
        _activityState.Begin(
            L("Vm_UndoActivityTitle"),
            L("Vm_UndoActivityStatus"),
            LF("Vm_UndoActivityStepFormat", selected.Count),
            operationId.ToString());
        AppendLog(LF("Vm_OperationStartedFormat", operationId), "INFO");
        var succeeded = 0;
        var failed = 0;
        try
        {
            foreach (var item in selected.Reverse())
            {
                var result = await _operationJournalUndoService.ExecuteUndoAsync(item.Item, _configurationService.ConfigDirectory);
                AppendLog(
                    result.Success
                        ? LF("Vm_UndoSucceededFormat", result.OperationId, result.Message)
                        : LF("Vm_UndoFailedFormat", result.OperationId, result.Message),
                    result.Success ? "SUCCESS" : "ERROR");
                if (result.Success) { succeeded++; } else { failed++; }
            }

            _activityOutcome = failed == 0 ? ActivityOutcome.Success : ActivityOutcome.Error;
            ActivityStatus = failed == 0 ? L("Vm_UndoComplete") : L("Vm_UndoNeedsReview");
            ActivityStep = LF("Vm_UndoResultFormat", succeeded, failed);
            ProgressValue = 100;
        }
        finally
        {
            IsRunning = false;
            OperationCorrelation.Complete(operationId, "WPF", "Undo", _activityOutcome.ToString());
            RefreshUndoActionItems();
            await RefreshSnapshotAsync();
        }
    }

    private async Task RefreshSnapshotAsync()
    {
        var requestVersion = Interlocked.Increment(ref _snapshotRequestVersion);
        SetSnapshotQueryState(isLoading: true, loadFailed: false);
        try
        {
            var snapshot = await _snapshotLoader(_configurationService.ConfigPath);
            if (requestVersion != Volatile.Read(ref _snapshotRequestVersion))
            {
                return;
            }

            _environmentState.Update(snapshot, DateTime.Now);
            SetSnapshotQueryState(isLoading: false, loadFailed: false);
            RaiseSnapshotInsightsChanged();
        }
        catch (Exception ex)
        {
            if (requestVersion != Volatile.Read(ref _snapshotRequestVersion))
            {
                return;
            }

            Serilog.Log.Warning(ex, "Environment snapshot refresh failed");
            SetSnapshotQueryState(isLoading: false, loadFailed: true);
            AppendLog(L("Vm_ShellSnapshotUnavailableDetail"), "ERROR");
        }
    }

    private void SetSnapshotQueryState(bool isLoading, bool loadFailed)
    {
        _isSnapshotLoading = isLoading;
        _snapshotLoadFailed = loadFailed;
        OnPropertyChanged(nameof(IsSnapshotLoading));
        OnPropertyChanged(nameof(HasSnapshotLoadError));
        OnPropertyChanged(nameof(IsEnvironmentReadyForActions));
        OnPropertyChanged(nameof(ShellReadinessPercent));
        OnPropertyChanged(nameof(ShellReadinessShortLabel));
        RaiseShellChromeChanged();
        RaiseRunCommandStateChanged();
    }

    private void RaiseSnapshotFreshnessChanged() => _environmentState.RefreshFreshness();

    public void PresentCloseWhileRunningPrompt(Func<Task> confirmAction)
    {
        if (!IsRunning)
        {
            return;
        }

        ShowPrompt(
            L("Vm_CloseWhileRunningTitle"),
            LF("Vm_CloseWhileRunningBody", Environment.NewLine + Environment.NewLine),
            L("Vm_CloseWhileRunningConfirm"),
            L("Vm_CloseWhileRunningCancel"),
            true,
            confirmAction,
            L("Vm_CloseWhileRunningSummaryTitle"),
            L("Vm_CloseWhileRunningSummaryBody"));
    }

    private void DismissActivity()
    {
        IsActivityVisible = false;
    }

    private void CopyLog()
    {
        if (LogEntries.Count == 0)
        {
            return;
        }

        var text = string.Join(Environment.NewLine, LogEntries.Select(e => e.CopyLine));

        if (!TrySetClipboardText(text))
        {
            AppendLog(L("Vm_ActivityLogClipboardUnavailable"), "WARN");
        }
    }

    private void CopyOperationId()
    {
        if (!HasActivityOperationId)
        {
            return;
        }

        var copied = TrySetClipboardText(ActivityOperationId);
        AppendLog(
            copied ? L("Vm_OperationIdCopied") : L("Vm_OperationIdClipboardUnavailable"),
            copied ? "INFO" : "WARN");
    }

    private bool TrySetClipboardText(string text)
    {
        // Clipboard is shared with other processes and can be briefly unavailable.
        // Try three times with a short yield before giving up so transient contention
        // (Office, clipboard managers, RDP) doesn't surface as a crash.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                if (attempt < 2)
                {
                    // Yield to the dispatcher instead of blocking the UI thread.
                    // This lets WPF process pending messages while we wait.
                    _dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => { }));
                    continue;
                }
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private void OpenLibreSpotFolder()
    {
        try
        {
            Directory.CreateDirectory(_configurationService.ConfigDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _configurationService.ConfigDirectory,
                UseShellExecute = true,
                WorkingDirectory = _configurationService.ConfigDirectory
            })?.Dispose();
        }
        catch (Exception ex)
        {
            AppendLog(LF("Vm_LogOpenFolderFailed", ex.Message), "WARN");
        }
    }

    private void ShowPrompt(
        string title,
        string body,
        string confirmText,
        string cancelText,
        bool isDestructive,
        Func<Task> confirmAction,
        string? summaryTitle = null,
        string? summaryBody = null)
    {
        _promptState.Show(
            title,
            body,
            confirmText,
            cancelText,
            isDestructive,
            confirmAction,
            summaryTitle,
            summaryBody);
    }

    private Task ConfirmPromptAsync() => _promptState.ConfirmAsync();

    private void CancelPrompt() => _promptState.Cancel();

    private void HandleEscape()
    {
        if (IsPromptVisible)
        {
            ClearPrompt();
            return;
        }

        if (HasGlobalSearchText)
        {
            GlobalSearchText = string.Empty;
            return;
        }

        if (IsActivityVisible && !IsRunning)
        {
            IsActivityVisible = false;
        }
    }

    private void ClearPrompt() => _promptState.Clear();

    private (string Title, string Body) BuildMaintenancePromptSummary(MaintenanceActionDefinition definition) =>
        definition.Action switch
        {
            "CheckUpdates" => (L("Vm_PromptWhatThisDoes"), L("Vm_MaintenanceSummaryCheckUpdates")),
            "ClearCache" => (L("Vm_PromptWhatThisDoes"), L("Vm_MaintenanceSummaryClearCache")),
            "Reapply" => (L("Vm_PromptWhatThisDoes"), L("Vm_MaintenanceSummaryReapply")),
            "RepairMarketplace" => (L("Vm_PromptWhatThisDoes"), L("Vm_MaintenanceSummaryRepairMarketplace")),
            "OpenMarketplace" => (L("Vm_PromptWhatThisDoes"), L("Vm_MaintenanceSummaryOpenMarketplace")),
            "RestoreVanilla" => (L("Vm_PromptWhatThisDoes"), L("Vm_MaintenanceSummaryRestoreVanilla")),
            "UninstallSpicetify" => (L("Vm_PromptWhatThisRemoves"), L("Vm_MaintenanceSummaryUninstallSpicetify")),
            "FullReset" => (L("Vm_PromptWhatThisRemoves"), L("Vm_MaintenanceSummaryFullReset")),
            "RemoveSelfData" => (L("Vm_PromptWhatThisRemoves"), L("Vm_MaintenanceSummaryRemoveSelfData")),
            _ => definition.IsDestructive
                ? (L("Vm_PromptWhatThisRemoves"), L("Vm_MaintenanceSummaryDefaultDestructive"))
                : (L("Vm_PromptWhatThisDoes"), L("Vm_MaintenanceSummaryDefaultStandard"))
        };

    /// <summary>
    /// Ensures the user has acknowledged the Spotify ToS risk before any
    /// patching action runs. Shows a blocking prompt on the first run and
    /// persists the acknowledgment to config.json so it never appears again.
    /// </summary>
    private async Task<bool> EnsureRiskAcknowledgedAsync()
    {
        try
        {
            var config = await _configurationService.LoadAsync();
            if (config.RiskAcknowledged)
            {
                return true;
            }
        }
        catch
        {
            // If we can't read config, assume not acknowledged.
        }

        var tcs = new TaskCompletionSource<bool>();

        ShowPrompt(
            L("Vm_RiskPromptTitle"),
            LF("Vm_RiskPromptBodyFormat", Environment.NewLine + Environment.NewLine),
            L("Vm_RiskPromptConfirm"),
            Strings.ButtonCancel,
            false,
            async () =>
            {
                // Signal acceptance synchronously before any await so the
                // deferred cancellation handler (OnPromptHidden) always
                // loses the TrySetResult race.
                tcs.TrySetResult(true);

                try
                {
                    var config = await _configurationService.LoadAsync();
                    config.RiskAcknowledged = true;
                    await _configurationService.SaveAsync(config);
                }
                catch
                {
                    // Best-effort save; the backend will re-check.
                }
            },
            L("Vm_RiskPromptSummaryTitle"),
            L("Vm_RiskPromptSummaryBody"));

        // The prompt is non-blocking UI; we need to wait for the user to act.
        // ConfirmPromptAsync calls ClearPrompt() (setting IsPromptVisible=false)
        // *before* running the action, so the PropertyChanged handler must use
        // TrySetResult(false) which becomes a no-op when the confirm action
        // already resolved the TCS with true.  We post the cancellation
        // resolution via the dispatcher so the confirm action lambda has a
        // chance to complete the TCS first.
        void OnPromptHidden(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IsPromptVisible) && !IsPromptVisible)
            {
                _dispatcher.InvokeAsync(() => tcs.TrySetResult(false), DispatcherPriority.Background);
            }
        }

        PropertyChanged += OnPromptHidden;
        try
        {
            return await tcs.Task;
        }
        finally
        {
            PropertyChanged -= OnPromptHidden;
        }
    }

    private void ShowNotice(string title, string status, string step, string? operationId = null)
    {
        _runStopwatch.Reset();
        _runElapsedTimer.Stop();
        _activityState.ShowNotice(title, status, step, operationId);
        OnPropertyChanged(nameof(RunElapsedText));
    }

    public void ApplyUiAutomationSmokeState(string state)
    {
        var normalizedState = state.Trim().ToLowerInvariant();
        _uiAutomationSafeModeRestoreAvailable = normalizedState == "maintenance-safe-mode";
        _uiAutomationMinidumpEnabled = normalizedState == "maintenance-minidump";
        OnPropertyChanged(nameof(IsSafeModeRestoreAvailable));
        OnPropertyChanged(nameof(IsSafeModeRestoreUnavailable));
        OnPropertyChanged(nameof(IsMinidumpEnabled));
        OnPropertyChanged(nameof(MinidumpStatusText));
        RefreshSupportBundlePreview();
        IsMaintenanceDiagnosticsExpanded = false;
        IsMaintenanceDangerExpanded = false;
        if (normalizedState is "recommended" or "home-navigation" or "home-details" or "home-readiness" or "reduced-motion" or "axe-positive-control")
        {
            ApplyUiAutomationHomeSnapshot("unmanaged");
        }
        else if (normalizedState is "home-healthy" or "home-repair" or "home-destructive")
        {
            ApplyUiAutomationHomeSnapshot(normalizedState);
        }
        else if (normalizedState is "maintenance" or "maintenance-safe-mode" or "maintenance-minidump" or "maintenance-compatibility" or "support-bundle")
        {
            ApplyUiAutomationHomeSnapshot("home-repair");
        }
        else if (normalizedState == "maintenance-healthy")
        {
            ApplyUiAutomationHomeSnapshot("home-healthy");
        }
        else if (normalizedState == "maintenance-danger")
        {
            ApplyUiAutomationHomeSnapshot("home-destructive");
        }
        else if (normalizedState == "home-update")
        {
            ApplyUiAutomationHomeSnapshot("home-healthy");
            _libreSpotUpdateNotice = new ReleaseNotice(true, "9.9.9", ReleaseNoticeService.LatestStableReleasePage, "smoke", "UI automation smoke state");
            RaiseLibreSpotUpdateNoticeChanged();
        }

        if (normalizedState is "recommended" or "custom" or "custom-live" or "maintenance" or "maintenance-safe-mode" or "maintenance-minidump" or "maintenance-compatibility" or "provenance" or "profile" or "support-bundle" or "activity-collapsed")
        {
            SeedUiAutomationActivityLog();
        }

        switch (normalizedState)
        {
            case "custom":
                SelectedWorkspaceIndex = 1;
                break;
            case "custom-profiles":
                SelectedWorkspaceIndex = 1;
                IsProfileToolsExpanded = true;
                break;
            case "custom-live":
                SelectedWorkspaceIndex = 1;
                SettingsSearchText = L("LiveCustomizationTitle");
                FeatureSearchText = string.Empty;
                break;
            case "maintenance":
            case "maintenance-safe-mode":
            case "maintenance-minidump":
                SelectedWorkspaceIndex = 2;
                IsMaintenanceDiagnosticsExpanded = normalizedState == "maintenance-minidump";
                break;
            case "maintenance-compatibility":
            case "support-bundle":
                SelectedWorkspaceIndex = 2;
                IsMaintenanceDiagnosticsExpanded = true;
                break;
            case "maintenance-healthy":
                SelectedWorkspaceIndex = 2;
                break;
            case "maintenance-error":
                SelectedWorkspaceIndex = 2;
                ClearLog();
                SetSnapshotQueryState(isLoading: false, loadFailed: true);
                break;
            case "maintenance-danger":
                SelectedWorkspaceIndex = 2;
                IsMaintenanceDangerExpanded = true;
                break;
            case "profile":
                SelectedWorkspaceIndex = 1;
                IsProfileToolsExpanded = true;
                break;
            case "activity-empty":
                SelectedWorkspaceIndex = 0;
                ClearLog();
                break;
            case "activity-collapsed":
                SelectedWorkspaceIndex = 0;
                break;
            case "snapshot-loading":
                SelectedWorkspaceIndex = 0;
                ClearLog();
                SetSnapshotQueryState(isLoading: true, loadFailed: false);
                break;
            case "snapshot-error":
                SelectedWorkspaceIndex = 0;
                ClearLog();
                SetSnapshotQueryState(isLoading: false, loadFailed: true);
                break;
            case "home-healthy":
            case "home-repair":
            case "home-destructive":
                SelectedWorkspaceIndex = 0;
                break;
            case "custom-no-results":
                SelectedWorkspaceIndex = 1;
                SettingsSearchText = "__no_matching_setting__";
                break;
            case "global-search":
                SelectedWorkspaceIndex = 0;
                GlobalSearchText = "marketplace";
                break;
            case "prompt":
                SelectedWorkspaceIndex = 0;
                ShowPrompt(
                    _localizationService.GetString("Ui_DecisionPrompt"),
                    _localizationService.GetString("Ui_ConfirmsAnImportantLibreSpotActionBeforeItRuns"),
                    Strings.ButtonContinue,
                    Strings.ButtonCancel,
                    false,
                    () => Task.CompletedTask,
                    Strings.MaintenanceCardDefaultDetail,
                    Strings.Maintenance_CheckUpdates_Description);
                break;
            case "prompt-destructive":
                SelectedWorkspaceIndex = 0;
                ShowPrompt(
                    Strings.PromptActionReset,
                    Strings.Maintenance_FullReset_Description,
                    Strings.Maintenance_FullReset_ButtonText,
                    Strings.ButtonCancel,
                    true,
                    () => Task.CompletedTask,
                    Strings.MaintenanceCardDestructiveDetail,
                    Strings.Maintenance_FullReset_Description);
                break;
            case "activity":
                SelectedWorkspaceIndex = 0;
                AppendLog("UI automation smoke activity.", "INFO");
                ShowNotice(
                    Strings.ActivityDialogName,
                    Strings.RunComplete,
                    Strings.ProgressSpotifyReady,
                    "11111111-1111-1111-1111-111111111111");
                ProgressValue = 100;
                break;
            case "activity-running":
                SelectedWorkspaceIndex = 0;
                _activityOutcome = ActivityOutcome.None;
                _activityState.Begin(
                    Strings.ActivityDialogName,
                    Strings.StatusInProgress,
                    Strings.PreparingBackend,
                    "22222222-2222-2222-2222-222222222222");
                ProgressValue = 42;
                AppendLog("UI automation smoke active run.", "INFO");
                break;
            case "activity-error":
                SelectedWorkspaceIndex = 0;
                _lastBackendAction = "Install";
                _lastBackendRunResult = new BackendRunResult(false, "UI automation smoke failure.", ErrorCode: "SmokeFailure");
                _lastRunStartedAt = DateTimeOffset.Now.AddSeconds(-7);
                _lastRunCompletedAt = DateTimeOffset.Now;
                AppendLog("UI automation smoke failure.", "ERROR");
                ShowNotice(
                    Strings.ActivityDialogName,
                    Strings.RunNeedsAttention,
                    "Backend reported an error",
                    "33333333-3333-3333-3333-333333333333");
                _activityOutcome = ActivityOutcome.Error;
                ProgressValue = 100;
                RaiseActivityDerivedStateChanged();
                break;
            case "activity-undo":
                SelectedWorkspaceIndex = 0;
                _activityState.ReplaceUndoActionItems(new[]
                {
                    new OperationJournalUndoItem(
                        "smoke",
                        "EnableAutoReapply",
                        "task",
                        "LibreSpot\\ReapplyWatcher",
                        "Registered",
                        "Unregister the scheduled task to undo.",
                        TokenKind: "ScheduledTask")
                });
                AppendLog(L("Vm_LibreSpotReady"), "INFO");
                ShowNotice(
                    Strings.RunComplete,
                    L("Vm_LibreSpotReady"),
                    Strings.ProgressSpotifyReady,
                    "44444444-4444-4444-4444-444444444444");
                ProgressValue = 100;
                break;
            default:
                SelectedWorkspaceIndex = 0;
                break;
        }
    }

    private void ApplyUiAutomationHomeSnapshot(string state)
    {
        StackHealthComponent Component(
            string id,
            string nameKey,
            string statusKey,
            string severity,
            string evidenceKey,
            params string[] actions) =>
            new(
                id,
                L(nameKey),
                L(statusKey),
                severity,
                null,
                null,
                null,
                L(evidenceKey),
                actions);

        var readyComponents = new[]
        {
            Component("spotify", "HealthNameSpotify", "HealthStatusDetected", HealthSeverity.Ready, "HealthEvidenceSpotifyDetected"),
            Component("spotx", "HealthNameSpotXPatch", "HealthStatusVerified", HealthSeverity.Ready, "HealthEvidenceSpotXVerified"),
            Component("spicetify-cli", "HealthNameSpicetifyCli", "HealthStatusDetected", HealthSeverity.Ready, "HealthEvidenceSpicetifyCliDetected")
        };

        var snapshot = state switch
        {
            "home-healthy" => new EnvironmentSnapshot
            {
                SpotifyInstalled = true,
                SpicetifyInstalled = true,
                HealthReport = new StackHealthReport(readyComponents)
            },
            "home-repair" => new EnvironmentSnapshot
            {
                SpotifyInstalled = true,
                SpicetifyInstalled = true,
                MarketplaceFilesPresent = true,
                MarketplaceRegistered = true,
                HealthReport = new StackHealthReport(readyComponents.Append(
                    Component(
                        "marketplace",
                        "HealthNameMarketplace",
                        "HealthStatusMarketplaceThemeInactive",
                        HealthSeverity.Warning,
                        "HealthEvidenceMarketplaceThemeInactive",
                        "RepairMarketplace")))
            },
            "home-destructive" => new EnvironmentSnapshot
            {
                SpotifyInstalled = true,
                SpicetifyInstalled = true,
                HealthReport = new StackHealthReport(readyComponents.Append(
                    Component(
                        "patcher-ownership",
                        "HealthNamePatcherOwnership",
                        "HealthStatusOwnershipForeign",
                        HealthSeverity.Critical,
                        "Maintenance_FullReset_Description",
                        "FullReset")))
            },
            _ => new EnvironmentSnapshot
            {
                HealthReport = new StackHealthReport(
                [
                    Component("spotify", "HealthNameSpotify", "HealthStatusNotInstalled", HealthSeverity.Info, "HealthEvidenceSpotifyMissing", "Install"),
                    Component("spotx", "HealthNameSpotXPatch", "HealthStatusNotChecked", HealthSeverity.Info, "HealthEvidenceSpotXNotChecked", "Install"),
                    Component("spicetify-cli", "HealthNameSpicetifyCli", "HealthStatusNotInstalled", HealthSeverity.Info, "HealthEvidenceSpicetifyCliMissing", "Install")
                ])
            }
        };

        _environmentState.Update(snapshot, DateTime.Now);
        SetSnapshotQueryState(isLoading: false, loadFailed: false);
        RaiseSnapshotInsightsChanged();
    }

    private void SeedUiAutomationActivityLog()
    {
        if (LogEntries.Count > 0)
        {
            return;
        }

        AppendLog(L("Vm_ShellLogEnvironmentReady"), "INFO");
        AppendLog(Snapshot.StatusDetail, "INFO");
        AppendLog(
            SelectedLocalProfile?.Name is { Length: > 0 } profileName
                ? LF("Vm_ShellLogUsingProfileFormat", profileName)
                : L("Vm_ShellLogUsingDefaultProfile"),
            "INFO");
        AppendLog(ShellReadinessDetail, "INFO");
    }

    private static bool IsAdministrator()
    {
        if (Environment.GetCommandLineArgs().Any(arg => arg.StartsWith("--uia-smoke=", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }


    private enum ActivityOutcome { None, Success, Warning, Error, Canceled }
}
