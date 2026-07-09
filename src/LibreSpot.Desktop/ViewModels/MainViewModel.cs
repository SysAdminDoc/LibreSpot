using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
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

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly ConfigurationService _configurationService;
    private readonly BackendScriptService _backendScriptService;
    private readonly EnvironmentSnapshotService _snapshotService;
    private readonly SupportBundleService _supportBundleService;
    private readonly OperationJournalUndoService _operationJournalUndoService;
    private readonly LocalProfileService _profileService;
    private readonly CustomPatchService _customPatchService;
    private readonly LocalizationService _localizationService;
    private readonly ISpotifyProcessService _spotifyProcessService;
    private readonly ActivityRunStateViewModel _activityState = new();
    private ActivityOutcome _activityOutcome = ActivityOutcome.None;
    private readonly CustomOptionEditorStateViewModel _customOptions;
    private readonly EnvironmentSnapshotStateViewModel _environmentState = new();
    private readonly PromptStateViewModel _promptState = new();
    private readonly SettingsSearchStateViewModel _settingsSearch = new();
    private readonly Dispatcher _dispatcher;
    private readonly bool _isAdministratorSession;
    private bool _resumeInstallAfterElevation;
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
    private bool _isSnapshotLoading = true;
    private bool _snapshotLoadFailed;

    private int _selectedWorkspaceIndex;
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
        ISpotifyProcessService? spotifyProcessService = null)
    {
        _configurationService = configurationService;
        _backendScriptService = backendScriptService;
        _snapshotService = snapshotService;
        _supportBundleService = supportBundleService ?? new SupportBundleService(configurationService.ConfigDirectory);
        _operationJournalUndoService = operationJournalUndoService ?? new OperationJournalUndoService();
        _profileService = profileService ?? new LocalProfileService(configurationService);
        _customPatchService = customPatchService ?? new CustomPatchService();
        _localizationService = localizationService ?? LocalizationService.Current;
        _spotifyProcessService = spotifyProcessService ?? new SpotifyProcessService();
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
        CancelRunCommand = new RelayCommand(CancelRunningBackend, () => IsRunning && !IsCancelRequested);
        DismissActivityCommand = new RelayCommand(DismissActivity, () => IsActivityVisible && !IsRunning);
        CopyLogCommand = new RelayCommand(CopyLog, () => LogEntries.Count > 0);
        ClearLogCommand = new RelayCommand(ClearLog, () => LogEntries.Count > 0);
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
        OpenSpicetifyCommunityCommand = new RelayCommand(() => OpenExternalUri("https://spicetify.app/docs/advanced-usage/extensions"));
        OpenThemeCatalogCommand = new RelayCommand(() => OpenExternalUri("https://github.com/spicetify/spicetify-themes"));
        ShowRecommendedWorkspaceCommand = new RelayCommand(() => SelectedWorkspaceIndex = 0);
        ShowCustomWorkspaceCommand = new RelayCommand(() => SelectedWorkspaceIndex = 1);
        ShowMaintenanceWorkspaceCommand = new RelayCommand(() => SelectedWorkspaceIndex = 2);
        EnableAutoReapplyCommand = new RelayCommand(() => PresentAutoReapplyPrompt(enable: true), () => !IsRunning && !Snapshot.AutoReapplyTaskRegistered);
        DisableAutoReapplyCommand = new RelayCommand(() => PresentAutoReapplyPrompt(enable: false), () => !IsRunning && Snapshot.AutoReapplyTaskRegistered);
        ClearSettingsSearchCommand = new RelayCommand(() => SettingsSearchText = string.Empty, () => HasSettingsSearchText);
        ClearThemeSearchCommand = new RelayCommand(() => ThemeSearchText = string.Empty, () => HasThemeSearchText);
        RelaunchAsAdministratorCommand = new RelayCommand(() => PresentAdministratorPrompt(), () => NeedsAdministratorRelaunch && !IsRunning);
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
    public ObservableCollection<MaintenanceActionCardViewModel> SafeMaintenanceActions => _maintenanceActions.SafeActions;
    public ObservableCollection<MaintenanceActionCardViewModel> DestructiveMaintenanceActions => _maintenanceActions.DestructiveActions;
    public ObservableCollection<SupportBundleCategoryViewModel> SupportBundleItems { get; }
    public ObservableCollection<string> SupportBundleRedactionRules { get; }
    public ObservableCollection<string> ChangelogHighlights { get; }
    public ObservableCollection<UndoActionItemViewModel> UndoActionItems => _activityState.UndoActionItems;
    public ObservableCollection<LocalProfileCardViewModel> LocalProfiles { get; }
    public ObservableCollection<string> CustomPatchFindings { get; }
    public ObservableCollection<LocalizationOption> LocalizationOptions { get; }
    public ObservableCollection<LogEntryViewModel> LogEntries => _activityState.LogEntries;

    public IAsyncRelayCommand ApplyRecommendedCommand { get; }
    public IAsyncRelayCommand ApplyCustomCommand { get; }
    public RelayCommand CancelRunCommand { get; }
    public RelayCommand DismissActivityCommand { get; }
    public RelayCommand CopyLogCommand { get; }
    public RelayCommand ClearLogCommand { get; }
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
    public RelayCommand RelaunchAsAdministratorCommand { get; }
    public IAsyncRelayCommand ConfirmPromptCommand { get; }
    public RelayCommand CancelPromptCommand { get; }
    public RelayCommand EscapeCommand { get; }

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
    public bool NeedsAdministratorRelaunch => !_isAdministratorSession;

    public LocalProfileCardViewModel? SelectedLocalProfile
    {
        get => _selectedLocalProfile;
        set
        {
            if (SetProperty(ref _selectedLocalProfile, value))
            {
                RefreshProfileFormFromSelection();
                RaiseLocalProfileStateChanged();
                _selectedProfileShareRefreshTask = RefreshSelectedProfileShareCardAsync();
            }
        }
    }

    public string ProfileNameText
    {
        get => _profileNameText;
        set
        {
            if (SetProperty(ref _profileNameText, value))
            {
                RaiseLocalProfileCommandStateChanged();
            }
        }
    }

    public string ProfileDescriptionText
    {
        get => _profileDescriptionText;
        set
        {
            if (SetProperty(ref _profileDescriptionText, value))
            {
                RaiseLocalProfileCommandStateChanged();
            }
        }
    }

    public string ProfileOperationStatus
    {
        get => _profileOperationStatus;
        private set => SetProperty(ref _profileOperationStatus, value);
    }

    public bool HasSelectedProfileShareCard => _selectedProfileShareCard is not null;

    public string SelectedProfileShareUri => _selectedProfileShareCard?.ShareUri ?? string.Empty;

    public ImageSource? SelectedProfileQrImage
    {
        get => _selectedProfileQrImage;
        private set => SetProperty(ref _selectedProfileQrImage, value);
    }

    public bool HasSelectedProfileQrImage => SelectedProfileQrImage is not null;

    public string SelectedProfileShareStatus
    {
        get => _selectedProfileShareStatus;
        private set => SetProperty(ref _selectedProfileShareStatus, value);
    }

    public string SelectedProfileComparisonText
    {
        get => _selectedProfileComparisonText;
        private set => SetProperty(ref _selectedProfileComparisonText, value);
    }

    public bool HasLocalProfiles => LocalProfiles.Count > 0;
    public bool HasSelectedLocalProfile => SelectedLocalProfile is not null;
    public bool CanEditSelectedLocalProfile => SelectedLocalProfile?.IsEditable == true;
    public string SelectedLocalProfileTitle => SelectedLocalProfile?.Name ?? L("Vm_ProfileNoSelectionTitle");
    public string SelectedLocalProfileDetail =>
        SelectedLocalProfile is null
            ? L("Vm_ProfileNoSelectionDetail")
            : SelectedLocalProfile.IsActive
                ? L("Vm_ProfileActiveDetail")
                : SelectedLocalProfile.Description;

    public string ProfileSelectionHint =>
        IsRunning
            ? L("Vm_ProfileSelectionPaused")
            : SelectedLocalProfile is null
                ? L("Vm_ProfileSelectionNone")
                : SelectedLocalProfile.IsBuiltIn
                    ? L("Vm_ProfileSelectionBuiltIn")
                    : SelectedLocalProfile.IsActive
                        ? L("Vm_ProfileSelectionActive")
                        : L("Vm_ProfileSelectionLocal");

    public string ProfileEditorHint =>
        SelectedLocalProfile is null
            ? L("Vm_ProfileEditorNoSelection")
            : SelectedLocalProfile.IsBuiltIn
                ? L("Vm_ProfileEditorBuiltIn")
                : L("Vm_ProfileEditorEditable");

    public string SessionAccessTitle =>
        IsAdministratorSession ? Strings.ReadyToRun : Strings.AdminStepNeeded;

    public string SessionAccessDetail =>
        IsAdministratorSession
            ? Strings.ReadyToRunDescription
            : Strings.AdminStepDescription;

    public string ShellReadinessTitle => L("Vm_ShellReadinessTitle");

    public string ShellReadinessValue =>
        IsSnapshotLoading
            ? L("Vm_ShellCheckingSystem")
            : HasSnapshotLoadError
                ? L("Vm_ShellSnapshotUnavailable")
                : NeedsAdministratorRelaunch
            ? Strings.AdminStepNeeded
            : HasCriticalHealthIssues
                ? Strings.RunNeedsAttention
                : L("Vm_ShellReadyToPatch");

    public string ShellReadinessDetail =>
        IsSnapshotLoading
            ? L("Vm_ShellCheckingSystemDetail")
            : HasSnapshotLoadError
                ? L("Vm_ShellSnapshotUnavailableDetail")
                : NeedsAdministratorRelaunch
            ? Strings.AdminStepDescription
            : HasCriticalHealthIssues
                ? HealthIssueSummary
                : L("Vm_ShellNoBlockingIssues");

    public string ShellReadinessPercent =>
        IsSnapshotLoading || HasSnapshotLoadError
            ? "—"
            : NeedsAdministratorRelaunch || HasCriticalHealthIssues ? "0%" : "100%";

    public string ShellReadinessShortLabel =>
        IsSnapshotLoading
            ? L("Vm_ShellCheckingShort")
            : HasSnapshotLoadError
                ? L("Vm_ShellRetryShort")
                : NeedsAdministratorRelaunch || HasCriticalHealthIssues
                    ? L("Vm_ShellNeedsReviewShort")
                    : Strings.SeverityReady;

    public bool IsSnapshotLoading => _isSnapshotLoading;
    public bool HasSnapshotLoadError => _snapshotLoadFailed;
    public bool IsEnvironmentReadyForActions => !IsSnapshotLoading && !HasSnapshotLoadError;

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
    public string ShellCheckStatusLabel =>
        IsSnapshotLoading || HasSnapshotLoadError || NeedsAdministratorRelaunch || HasCriticalHealthIssues
            ? ShellReadinessShortLabel
            : ShellCheckOkLabel;
    public string ShellVerifyEnvironmentTitle => L("Vm_ShellVerifyEnvironmentTitle");
    public string ShellVerifyEnvironmentDetail => L("Vm_ShellVerifyEnvironmentDetail");
    public string ShellRepairTitle => L("Vm_ShellRepairTitle");
    public string ShellRepairDetail => L("Vm_ShellRepairDetail");
    public string ShellClearCacheTitle => L("Vm_ShellClearCacheTitle");
    public string ShellClearCacheDetail => L("Vm_ShellClearCacheDetail");
    public string ShellTrustRiskTitle => L("Vm_ShellTrustRiskTitle");
    public string ShellTrustedSourcesTitle => L("Vm_ShellTrustedSourcesTitle");
    public string ShellTrustedSourcesDetail => L("Vm_ShellTrustedSourcesDetail");
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
    public string ShellDisplayVersion => "v4.0.0-preview.15";
    public string ShellUpdateStatusTitle => Snapshot.SpicetifyInstalled || Snapshot.SpotifyInstalled
        ? L("Vm_ShellUpdateReady")
        : L("Vm_ShellUpdateCurrent");
    public string ShellUpdateStatusDetail => Snapshot.SpicetifyInstalled || Snapshot.SpotifyInstalled
        ? L("Vm_ShellUpdateMaintenanceAvailable")
        : L("Vm_ShellUpdateLatestPreview");
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

    private string ShellOperatingSystemName
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                var version = Environment.OSVersion.Version;
                if (version.Major >= 10 && version.Build >= 22000)
                {
                    return L("Vm_ShellWindows11");
                }

                if (version.Major >= 10)
                {
                    return L("Vm_ShellWindows10");
                }
            }

            return RuntimeInformation.OSDescription;
        }
    }

    private string ShellOperatingSystemDetail =>
        OperatingSystem.IsWindows()
            ? LF("Vm_ShellOsBuildFormat", Environment.OSVersion.Version.Build)
            : RuntimeInformation.OSArchitecture.ToString();

    public IReadOnlyList<ShellSummaryItemViewModel> ShellSummaryItems =>
    [
        new(L("Vm_ShellSummaryStatus"), ShellReadinessValue, ShellReadinessDetail, "status", ShellReadinessTone),
        new(
            L("Vm_ShellSpotifyTargetLabel"),
            Snapshot.SpotifyInstalled ? L("Vm_ShellSpotifyInstalled") : L("Vm_ShellSpotifyNotDetected"),
            FirstNonEmpty(HealthComponent("spotify")?.DetectedVersion, ShellSpotifyTargetDetail),
            "spotify",
            Snapshot.SpotifyInstalled ? HealthSeverity.Ready : HealthSeverity.Info),
        new(
            L("Vm_ShellSummaryOs"),
            ShellOperatingSystemName,
            ShellOperatingSystemDetail,
            "os",
            HealthSeverity.Ready),
        new(
            L("Vm_ShellSummaryLastRun"),
            LogEntries.LastOrDefault()?.TimestampDisplay ?? L("Vm_ShellNotRunYet"),
            SelectedLocalProfile?.Name is { Length: > 0 } profileName ? LF("Vm_ShellProfileNameFormat", profileName) : L("Vm_ShellNoSetupRunYet"),
            "clock",
            HealthSeverity.Info)
    ];

    public IReadOnlyList<ShellEnvironmentRowViewModel> ShellEnvironmentRows =>
    [
        new(L("Vm_EnvUser"), Environment.UserName, HealthSeverity.Ready),
        new(L("Vm_EnvMachine"), Environment.MachineName, HealthSeverity.Ready),
        new(L("Vm_EnvWorkingDirectory"), Environment.CurrentDirectory, HealthSeverity.Ready),
        new(L("Vm_EnvPermissions"), IsAdministratorSession ? L("Vm_EnvAdministrator") : L("Vm_EnvStandardUser"), IsAdministratorSession ? HealthSeverity.Ready : HealthSeverity.Warning)
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
            : HasSnapshotLoadError || NeedsAdministratorRelaunch
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
    public bool HasWarningHealthIssues => HealthReport.HasWarningIssues;
    public bool HasInfoHealthIssues => HealthReport.HasInfoIssues;
    public bool HasAnyHealthIssues => HealthReport.HasIssues;
    public string HealthIssueSummary => HealthReport.IssueSummary;
    public bool HasUndoActionItems => _activityState.HasUndoActionItems;

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

    public string WorkspaceRecommendationDetail =>
        HasConfigurationRecoveryNotice
            ? L("Vm_WorkspaceRecommendationRecoverDetail")
            : Snapshot.SpotifyInstalled && Snapshot.SpicetifyInstalled
            ? L("Vm_WorkspaceRecommendationMaintainDetail")
            : Snapshot.SpotifyInstalled
                ? L("Vm_WorkspaceRecommendationFinishDetail")
                : L("Vm_WorkspaceRecommendationStartDetail");

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

    public string EnabledToggleCountLabel =>
        LF("Vm_EnabledToggleCountFormat", EnumerateAllOptions().Count(option => option.IsSelected));

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

    public string SelectedCustomAppCountLabel
    {
        get
        {
            var selectedCount = CustomApps.Count(item => item.IsSelected);
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

    public string AccessPostureLabel =>
        NeedsAdministratorRelaunch
            ? L("Vm_AccessPostureElevatesFirst")
            : L("Vm_AccessPostureCurrentSession");

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

    public bool HasVisibleInstallOptions => HasVisibleOptions(InstallOptions);

    public bool HasVisibleAppearanceSettings => CountAppearanceMatches() > 0;

    public bool HasVisibleCoreOptions => HasVisibleOptions(CoreOptions);

    public bool HasVisibleInterfaceOptions => HasVisibleOptions(InterfaceOptions);

    public bool HasVisibleBehaviorSection => HasVisibleCoreOptions || HasVisibleInterfaceOptions;

    public bool HasVisibleAdvancedOptions => HasVisibleOptions(AdvancedOptions);

    public bool HasVisibleExperienceOptions => HasVisibleOptions(ExperienceOptions);

    public bool HasVisibleAdvancedSection => HasVisibleAdvancedOptions || HasVisibleExperienceOptions || HasVisibleCustomPatchesSection;

    public bool HasVisibleExtensions => Extensions.Any(extension => MatchesSettingsSearch(extension.Title, extension.Description));

    public bool HasVisibleCustomApps => CustomApps.Any(app => MatchesSettingsSearch(app.Title, app.Description));

    public int CustomSearchMatchCount =>
        CountMatchingOptions(InstallOptions) +
        CountAppearanceMatches() +
        CountMatchingOptions(CoreOptions) +
        CountMatchingOptions(InterfaceOptions) +
        CountMatchingOptions(AdvancedOptions) +
        CountMatchingOptions(ExperienceOptions) +
        (HasVisibleCustomPatchesSection ? 1 : 0) +
        Extensions.Count(extension => MatchesSettingsSearch(extension.Title, extension.Description)) +
        CustomApps.Count(app => MatchesSettingsSearch(app.Title, app.Description));

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

    public string MaintenanceGuidanceTitle =>
        Snapshot.SpotifyInstalled && Snapshot.SpicetifyInstalled
            ? L("Vm_MaintenanceReadyTitle")
            : Snapshot.SpotifyInstalled
                ? L("Vm_MaintenanceIncompleteTitle")
                : L("Vm_MaintenanceReadyWhenNeededTitle");

    public string MaintenanceGuidanceDetail =>
        Snapshot.SpotifyInstalled && Snapshot.SpicetifyInstalled
            ? L("Vm_MaintenanceReadyDetail")
            : Snapshot.SpotifyInstalled
                ? L("Vm_MaintenanceIncompleteDetail")
                : L("Vm_MaintenanceReadyWhenNeededDetail");

    private int MaintenanceReadyComponentCount =>
        new[] { "spotify", "spotx", "spicetify-cli", "marketplace", "active-theme" }
            .Count(id => HealthComponent(id)?.Severity == HealthSeverity.Ready);

    public string MaintenanceReadinessValue => LF("Vm_MaintenanceReadinessValueFormat", MaintenanceReadyComponentCount);

    public string MaintenanceReadinessDetail =>
        MaintenanceReadyComponentCount switch
        {
            5 => L("Vm_MaintenanceReadinessAllReady"),
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
            if (NeedsAdministratorRelaunch)
            {
                return L("Vm_CustomReadinessAdminFirst");
            }

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
            if (NeedsAdministratorRelaunch)
            {
                return L("Vm_CustomReadinessAdminDetail");
            }

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
        NeedsAdministratorRelaunch
            ? L("Vm_CustomApplyAdmin")
            : HasConfigurationRecoveryNotice
                ? L("Vm_CustomApplyRecovery")
            : HasConflictingSidebarOptions()
                ? L("Vm_CustomApplyConflict")
            : CustomPatchesEnabled && !_customPatchValidation.IsValid
                ? L("Vm_CustomApplyPatchJson")
            : L("Vm_CustomApplyReady");

    public bool IsOverviewWorkspaceSelected => SelectedWorkspaceIndex == 0;

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
        _ => L("Vm_WorkspaceHeroRecommendedBody")
    };

    public int SelectedWorkspaceIndex
    {
        get => _selectedWorkspaceIndex;
        set
        {
            if (SetProperty(ref _selectedWorkspaceIndex, value))
            {
                OnPropertyChanged(nameof(IsOverviewWorkspaceSelected));
                OnPropertyChanged(nameof(ShowRailRunDuration));
                OnPropertyChanged(nameof(WorkspaceHeroEyebrow));
                OnPropertyChanged(nameof(WorkspaceHeroTitle));
                OnPropertyChanged(nameof(WorkspaceHeroBody));
                OnPropertyChanged(nameof(ShowRecommendedRunBand));
            }
        }
    }

    // The Recommended hero already shows RecommendedRunDuration in the main
    // pane, so repeating it in the always-visible rail on that workspace is a
    // verbatim on-screen duplicate. Keep the rail hint on Custom/Maintenance,
    // where the rail is the only place the timing is surfaced.
    public bool ShowRailRunDuration => !IsOverviewWorkspaceSelected;

    public string SelectedTheme
    {
        get => _customOptions.SelectedTheme;
        set => _customOptions.SelectedTheme = value;
    }

    public ThemeGalleryItemViewModel? SelectedThemeGalleryItem
    {
        get => _customOptions.SelectedThemeGalleryItem;
        set => _customOptions.SelectedThemeGalleryItem = value;
    }

    public string ThemeSearchText
    {
        get => _customOptions.ThemeSearchText;
        set => _customOptions.ThemeSearchText = value;
    }

    public IReadOnlyList<ThemeGalleryItemViewModel> FilteredThemeGalleryItems => _customOptions.FilteredThemeGalleryItems;

    public bool HasThemeSearchText => _customOptions.HasThemeSearchText;
    public bool ShowThemeGalleryEmptyState => _customOptions.ShowThemeGalleryEmptyState;

    public string ThemeGalleryEmptyText => _customOptions.ThemeGalleryEmptyText;

    public string SelectedScheme
    {
        get => _customOptions.SelectedScheme;
        set => _customOptions.SelectedScheme = value;
    }

    public string SelectedLyricsTheme
    {
        get => _customOptions.SelectedLyricsTheme;
        set => _customOptions.SelectedLyricsTheme = value;
    }

    public string SelectedSpotifyVersionId
    {
        get => _customOptions.SelectedSpotifyVersionId;
        set => _customOptions.SelectedSpotifyVersionId = value;
    }

    public string SelectedDownloadMethod
    {
        get => _customOptions.SelectedDownloadMethod;
        set => _customOptions.SelectedDownloadMethod = value;
    }

    public string CacheLimitText
    {
        get => _customOptions.CacheLimitText;
        set => _customOptions.CacheLimitText = value;
    }

    public bool CustomPatchesEnabled
    {
        get => _customPatchesEnabled;
        set
        {
            if (SetProperty(ref _customPatchesEnabled, value))
            {
                RefreshCustomPatchValidation();
                RaiseSelectionInsightsChanged();
            }
        }
    }

    public string CustomPatchesJson
    {
        get => _customPatchesJson;
        set
        {
            if (SetProperty(ref _customPatchesJson, value ?? string.Empty))
            {
                if (!_preserveCustomPatchProvenance)
                {
                    ClearCustomPatchProvenance();
                }

                RefreshCustomPatchValidation();
                RaiseSelectionInsightsChanged();
            }
        }
    }

    public string CustomPatchesImportUrl
    {
        get => _customPatchesImportUrl;
        set
        {
            if (SetProperty(ref _customPatchesImportUrl, value ?? string.Empty))
            {
                ImportCustomPatchesFromUrlCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string CustomPatchesStatus => _customPatchValidation.Summary;

    public bool HasCustomPatchImportProvenance => !string.IsNullOrWhiteSpace(_customPatchesSourceSha256);

    public string CustomPatchesImportProvenance =>
        HasCustomPatchImportProvenance
            ? LF("Vm_CustomPatchesImportProvenanceFormat", _customPatchesSourceUrl, _customPatchesFetchedAtUtc, FormatBytes(_customPatchesSourceByteCount), _customPatchesSourceSha256)
            : string.Empty;

    public string CustomPatchesBadge =>
        !CustomPatchesEnabled
            ? L("Vm_CustomPatchesOff")
            : _customPatchValidation.IsValid
                ? Strings.SeverityReady
                : L("Vm_CustomPatchesNeedsReview");

    public string CustomPatchesSummary =>
        !CustomPatchesEnabled
            ? L("Vm_CustomPatchesSummaryOff")
            : _customPatchValidation.IsValid
                ? LF("Vm_CustomPatchesSummaryReadyFormat", _customPatchValidation.PatchGroupCount, _customPatchValidation.PatternCount, _customPatchValidation.ReplacementCount)
                : LF("Vm_CustomPatchesSummaryErrorFormat", _customPatchValidation.Errors.Count);

    public bool HasCustomPatchFindings => CustomPatchFindings.Count > 0;

    public bool HasVisibleCustomPatchesSection =>
        MatchesSettingsSearch(L("Vm_CustomPatchesSearchTitle"), L("Vm_CustomPatchesSearchDescription"));

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
                RaiseActivityDerivedStateChanged();
                break;
            case nameof(ActivityRunStateViewModel.Step):
                OnPropertyChanged(nameof(ActivityStep));
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
        }
    }

    private void RaiseRunCommandStateChanged()
    {
        ApplyRecommendedCommand.NotifyCanExecuteChanged();
        ApplyCustomCommand.NotifyCanExecuteChanged();
        CancelRunCommand.NotifyCanExecuteChanged();
        DismissActivityCommand.NotifyCanExecuteChanged();
        EnableAutoReapplyCommand.NotifyCanExecuteChanged();
        DisableAutoReapplyCommand.NotifyCanExecuteChanged();
        ExportSupportBundleCommand.NotifyCanExecuteChanged();
        ExportFailureBundleCommand.NotifyCanExecuteChanged();
        ClearAssetCacheCommand.NotifyCanExecuteChanged();
        RelaunchAsAdministratorCommand.NotifyCanExecuteChanged();
        ConfirmPromptCommand.NotifyCanExecuteChanged();
        CancelPromptCommand.NotifyCanExecuteChanged();
        ValidateCustomPatchesCommand.NotifyCanExecuteChanged();
        FormatCustomPatchesCommand.NotifyCanExecuteChanged();
        ClearCustomPatchesCommand.NotifyCanExecuteChanged();
        ImportCustomPatchesFromUrlCommand.NotifyCanExecuteChanged();
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
        OnPropertyChanged(nameof(ShellUpdateStatusTitle));
        OnPropertyChanged(nameof(ShellUpdateStatusDetail));
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
        OnPropertyChanged(nameof(ShellCheckStatusLabel));
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
        _dispatcher.BeginInvoke(RaiseLocalizedTextChanged, DispatcherPriority.Background);

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
        OnPropertyChanged(nameof(ShellCheckStatusLabel));
        OnPropertyChanged(nameof(ShellVerifyEnvironmentTitle));
        OnPropertyChanged(nameof(ShellVerifyEnvironmentDetail));
        OnPropertyChanged(nameof(ShellRepairTitle));
        OnPropertyChanged(nameof(ShellRepairDetail));
        OnPropertyChanged(nameof(ShellClearCacheTitle));
        OnPropertyChanged(nameof(ShellClearCacheDetail));
        OnPropertyChanged(nameof(ShellTrustRiskTitle));
        OnPropertyChanged(nameof(ShellTrustedSourcesTitle));
        OnPropertyChanged(nameof(ShellTrustedSourcesDetail));
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
        await RefreshLocalProfilesAsync();
        await RefreshSnapshotAsync();
    }

    private void ConfigureSettingsSearchFilters()
    {
        ConfigureOptionFilter(InstallOptions);
        ConfigureOptionFilter(CoreOptions);
        ConfigureOptionFilter(InterfaceOptions);
        ConfigureOptionFilter(AdvancedOptions);
        ConfigureOptionFilter(ExperienceOptions);
        CollectionViewSource.GetDefaultView(Extensions).Filter = item =>
            item is ExtensionToggleViewModel extension &&
            MatchesSettingsSearch(extension.Title, extension.Description);
        CollectionViewSource.GetDefaultView(CustomApps).Filter = item =>
            item is ExtensionToggleViewModel customApp &&
            MatchesSettingsSearch(customApp.Title, customApp.Description);
    }

    private void ConfigureOptionFilter(ObservableCollection<OptionToggleViewModel> options) =>
        CollectionViewSource.GetDefaultView(options).Filter = item =>
            item is OptionToggleViewModel option &&
            MatchesSettingsSearch(option.Title, option.Description);

    private void RefreshSettingsSearch()
    {
        CollectionViewSource.GetDefaultView(InstallOptions).Refresh();
        CollectionViewSource.GetDefaultView(CoreOptions).Refresh();
        CollectionViewSource.GetDefaultView(InterfaceOptions).Refresh();
        CollectionViewSource.GetDefaultView(AdvancedOptions).Refresh();
        CollectionViewSource.GetDefaultView(ExperienceOptions).Refresh();
        CollectionViewSource.GetDefaultView(Extensions).Refresh();
        CollectionViewSource.GetDefaultView(CustomApps).Refresh();
        ClearSettingsSearchCommand.NotifyCanExecuteChanged();
        RaiseCustomSearchChanged();
    }

    private void RaiseCustomSearchChanged()
    {
        OnPropertyChanged(nameof(HasSettingsSearchText));
        OnPropertyChanged(nameof(HasVisibleInstallOptions));
        OnPropertyChanged(nameof(HasVisibleAppearanceSettings));
        OnPropertyChanged(nameof(HasVisibleCoreOptions));
        OnPropertyChanged(nameof(HasVisibleInterfaceOptions));
        OnPropertyChanged(nameof(HasVisibleBehaviorSection));
        OnPropertyChanged(nameof(HasVisibleAdvancedOptions));
        OnPropertyChanged(nameof(HasVisibleExperienceOptions));
        OnPropertyChanged(nameof(HasVisibleCustomPatchesSection));
        OnPropertyChanged(nameof(HasVisibleAdvancedSection));
        OnPropertyChanged(nameof(HasVisibleExtensions));
        OnPropertyChanged(nameof(HasVisibleCustomApps));
        OnPropertyChanged(nameof(CustomSearchMatchCount));
        OnPropertyChanged(nameof(HasAnyCustomSearchMatches));
        OnPropertyChanged(nameof(ShowCustomSearchEmptyState));
        OnPropertyChanged(nameof(CustomSearchSummary));
    }

    private int CountMatchingOptions(IEnumerable<OptionToggleViewModel> options) =>
        options.Count(option => MatchesSettingsSearch(option.Title, option.Description));

    private bool HasVisibleOptions(IEnumerable<OptionToggleViewModel> options) =>
        options.Any(option => MatchesSettingsSearch(option.Title, option.Description));

    private int CountAppearanceMatches()
    {
        var count = 0;
        count += MatchesSettingsSearch(L("Vm_SearchThemePackTitle"), L("Vm_SearchThemePackDescription")) ? 1 : 0;
        count += MatchesSettingsSearch(L("Vm_SearchColorSchemeTitle"), ThemeSchemeHint) ? 1 : 0;
        count += MatchesSettingsSearch(L("Vm_SearchLyricsThemeTitle"), LyricsThemeHint) ? 1 : 0;
        count += MatchesSettingsSearch(L("Vm_SearchCacheLimitTitle"), L("Vm_SearchCacheLimitDescription")) ? 1 : 0;
        count += MatchesSettingsSearch(L("Vm_SearchSpotifyBuildTitle"), SpotifyVersionNotes) ? 1 : 0;
        count += MatchesSettingsSearch(L("Vm_SearchDownloadPathTitle"), DownloadMethodDetail) ? 1 : 0;
        return count;
    }

    private bool MatchesSettingsSearch(string title, string description)
        => _settingsSearch.Matches(title, description);

    private void RefreshCustomPatchValidation()
    {
        _customPatchValidation = _customPatchService.Validate(CustomPatchesJson, CustomPatchesEnabled);
        CustomPatchFindings.Clear();
        foreach (var finding in _customPatchValidation.Findings)
        {
            CustomPatchFindings.Add(finding);
        }

        RaiseCustomPatchStateChanged();
    }

    private void RaiseCustomPatchStateChanged()
    {
        OnPropertyChanged(nameof(CustomPatchesStatus));
        OnPropertyChanged(nameof(CustomPatchesBadge));
        OnPropertyChanged(nameof(CustomPatchesSummary));
        OnPropertyChanged(nameof(HasCustomPatchFindings));
        ValidateCustomPatchesCommand.NotifyCanExecuteChanged();
        FormatCustomPatchesCommand.NotifyCanExecuteChanged();
        ClearCustomPatchesCommand.NotifyCanExecuteChanged();
        ImportCustomPatchesFromUrlCommand.NotifyCanExecuteChanged();
    }

    private void RegisterOptionStateObservers()
    {
        foreach (var option in EnumerateAllOptions())
        {
            option.PropertyChanged += OnSelectionItemPropertyChanged;
        }

        foreach (var extension in Extensions)
        {
            extension.PropertyChanged += OnSelectionItemPropertyChanged;
        }

        foreach (var customApp in CustomApps)
        {
            customApp.PropertyChanged += OnSelectionItemPropertyChanged;
        }
    }

    private void OnSelectionItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OptionToggleViewModel.IsSelected) ||
            e.PropertyName == nameof(ExtensionToggleViewModel.IsSelected))
        {
            if (_isApplyingSelectionDependencyRules)
            {
                return;
            }

            ApplySelectionDependencyRules(sender as OptionToggleViewModel);
            RaiseSelectionInsightsChanged();
        }
    }

    private void ApplySelectionDependencyRules(OptionToggleViewModel? changedOption)
    {
        _isApplyingSelectionDependencyRules = true;
        try
        {
            var lyricsEnabled = FindOption(nameof(InstallConfiguration.SpotX_LyricsEnabled));
            var lyricsBlock = FindOption(nameof(InstallConfiguration.SpotX_LyricsBlock));
            var oldLyrics = FindOption(nameof(InstallConfiguration.SpotX_OldLyrics));

            if (lyricsEnabled?.IsSelected == false)
            {
                if (lyricsBlock is not null)
                {
                    lyricsBlock.IsSelected = false;
                }

                if (oldLyrics is not null)
                {
                    oldLyrics.IsSelected = false;
                }
            }
            else if (IsChangedOption(changedOption, nameof(InstallConfiguration.SpotX_LyricsBlock)) &&
                     lyricsBlock?.IsSelected == true)
            {
                if (oldLyrics is not null)
                {
                    oldLyrics.IsSelected = false;
                }
            }
            else if (IsChangedOption(changedOption, nameof(InstallConfiguration.SpotX_OldLyrics)) &&
                     oldLyrics?.IsSelected == true)
            {
                if (lyricsBlock is not null)
                {
                    lyricsBlock.IsSelected = false;
                }
            }
            else if (lyricsBlock?.IsSelected == true && oldLyrics?.IsSelected == true)
            {
                oldLyrics.IsSelected = false;
            }

            var rightSidebarOff = FindOption(nameof(InstallConfiguration.SpotX_RightSidebarOff));
            var rightSidebarColor = FindOption(nameof(InstallConfiguration.SpotX_RightSidebarClr));
            if (rightSidebarOff?.IsSelected == true && rightSidebarColor?.IsSelected == true)
            {
                rightSidebarColor.IsSelected = false;
            }
        }
        finally
        {
            _isApplyingSelectionDependencyRules = false;
        }
    }

    private IEnumerable<OptionToggleViewModel> EnumerateAllOptions() => _customOptions.EnumerateAllOptions();

    private void RaiseSelectionInsightsChanged()
    {
        RebuildSelectionInsights();
        OnPropertyChanged(nameof(CustomSelectionSummary));
        OnPropertyChanged(nameof(InstallPostureLabel));
        OnPropertyChanged(nameof(EnabledToggleCountLabel));
        OnPropertyChanged(nameof(IsThemeSchemeAvailable));
        OnPropertyChanged(nameof(ThemeSchemeHint));
        OnPropertyChanged(nameof(ThemeSummary));
        OnPropertyChanged(nameof(IsLyricsThemeAvailable));
        OnPropertyChanged(nameof(LyricsThemeHint));
        OnPropertyChanged(nameof(LyricsSummary));
        OnPropertyChanged(nameof(CacheSummary));
        OnPropertyChanged(nameof(SpotifyVersionSummary));
        OnPropertyChanged(nameof(SpotifyVersionNotes));
        OnPropertyChanged(nameof(ArchitectureMismatchWarning));
        OnPropertyChanged(nameof(HasArchitectureMismatch));
        OnPropertyChanged(nameof(DownloadMethodSummary));
        OnPropertyChanged(nameof(DownloadMethodDetail));
        OnPropertyChanged(nameof(CustomPatchesBadge));
        OnPropertyChanged(nameof(CustomPatchesSummary));
        OnPropertyChanged(nameof(ExtensionSummary));
        OnPropertyChanged(nameof(SelectedExtensionCountLabel));
        OnPropertyChanged(nameof(SelectedCustomAppCountLabel));
        OnPropertyChanged(nameof(HasSelectedExtensions));
        OnPropertyChanged(nameof(AccessPostureLabel));
        OnPropertyChanged(nameof(CustomChangeCountLabel));
        OnPropertyChanged(nameof(CustomProfileTitle));
        OnPropertyChanged(nameof(CustomProfileDetail));
        OnPropertyChanged(nameof(CustomRunReadinessTitle));
        OnPropertyChanged(nameof(CustomRunReadinessDetail));
        OnPropertyChanged(nameof(CustomApplyCaption));
    }

    private void RaiseSnapshotInsightsChanged()
    {
        RefreshMaintenanceActionRelevance();
        OnPropertyChanged(nameof(SessionAccessTitle));
        OnPropertyChanged(nameof(SessionAccessDetail));
        RaiseShellChromeChanged();
        OnPropertyChanged(nameof(SpotifyStatusLine));
        OnPropertyChanged(nameof(CustomizationStatusLine));
        OnPropertyChanged(nameof(MarketplaceStatusLine));
        OnPropertyChanged(nameof(HealthReport));
        OnPropertyChanged(nameof(CriticalHealthIssues));
        OnPropertyChanged(nameof(WarningHealthIssues));
        OnPropertyChanged(nameof(InfoHealthIssues));
        OnPropertyChanged(nameof(HasCriticalHealthIssues));
        OnPropertyChanged(nameof(HasWarningHealthIssues));
        OnPropertyChanged(nameof(HasInfoHealthIssues));
        OnPropertyChanged(nameof(HasAnyHealthIssues));
        OnPropertyChanged(nameof(HealthIssueSummary));
        OnPropertyChanged(nameof(StatusDashboardItems));
        OnPropertyChanged(nameof(ShellPrimaryStatusItems));
        OnPropertyChanged(nameof(MaintenanceReadinessValue));
        OnPropertyChanged(nameof(MaintenanceReadinessDetail));
        OnPropertyChanged(nameof(MaintenanceBackupValue));
        OnPropertyChanged(nameof(MaintenanceBackupDetail));
        OnPropertyChanged(nameof(MaintenanceMarketplaceValue));
        OnPropertyChanged(nameof(MaintenanceMarketplaceDetail));
        OnPropertyChanged(nameof(MaintenanceThemeValue));
        OnPropertyChanged(nameof(MaintenanceThemeDetail));
        RaiseSupportBundlePreviewChanged();
        OnPropertyChanged(nameof(HasConfigurationRecoveryNotice));
        OnPropertyChanged(nameof(ConfigurationRecoveryTitle));
        OnPropertyChanged(nameof(ConfigurationRecoveryDetail));
        OnPropertyChanged(nameof(ProfileStatusLine));
        OnPropertyChanged(nameof(WorkspaceRecommendationTitle));
        OnPropertyChanged(nameof(WorkspaceRecommendationDetail));
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
        RebuildSelectionInsights();
    }

    private async Task RefreshLocalProfilesAsync(string? preferredProfileId = null)
    {
        var selectedId = preferredProfileId ?? SelectedLocalProfile?.Id;
        var summaries = await _profileService.GetProfilesAsync();
        var orderedSummaries = summaries
            .OrderByDescending(summary => summary.IsActive)
            .ThenBy(summary => summary.IsBuiltIn ? 0 : 1)
            .ThenBy(summary => summary.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        LocalProfiles.Clear();
        foreach (var summary in orderedSummaries)
        {
            LocalProfiles.Add(new LocalProfileCardViewModel(summary));
        }

        SelectedLocalProfile =
            LocalProfiles.FirstOrDefault(profile => string.Equals(profile.Id, selectedId, StringComparison.OrdinalIgnoreCase)) ??
            LocalProfiles.FirstOrDefault(profile => profile.IsActive) ??
            LocalProfiles.FirstOrDefault();
        await _selectedProfileShareRefreshTask;

        ProfileOperationStatus = LocalProfiles.Count == 0
            ? L("Vm_ProfileNoChoicesStatus")
            : LocalProfiles.Count == 1
                ? L("Vm_ProfileOneChoiceReady")
                : LF("Vm_ProfileManyChoicesReadyFormat", LocalProfiles.Count);
        RaiseLocalProfileStateChanged();
    }

    private void RefreshProfileFormFromSelection()
    {
        if (SelectedLocalProfile is null)
        {
            ProfileNameText = L("Vm_ProfileDefaultName");
            ProfileDescriptionText = L("Vm_ProfileDefaultDescription");
            return;
        }

        ProfileNameText = SelectedLocalProfile.IsBuiltIn
            ? CreateCopyName(SelectedLocalProfile.Name)
            : SelectedLocalProfile.Name;
        ProfileDescriptionText = SelectedLocalProfile.Description;
    }

    private void RaiseLocalProfileStateChanged()
    {
        OnPropertyChanged(nameof(HasLocalProfiles));
        OnPropertyChanged(nameof(HasSelectedLocalProfile));
        OnPropertyChanged(nameof(CanEditSelectedLocalProfile));
        OnPropertyChanged(nameof(SelectedLocalProfileTitle));
        OnPropertyChanged(nameof(SelectedLocalProfileDetail));
        OnPropertyChanged(nameof(ProfileSelectionHint));
        OnPropertyChanged(nameof(ProfileEditorHint));
        OnPropertyChanged(nameof(ShellSummaryItems));
        OnPropertyChanged(nameof(ShellActivityLogItems));
        RaiseLocalProfileCommandStateChanged();
    }

    private void RaiseLocalProfileCommandStateChanged()
    {
        RefreshProfilesCommand.NotifyCanExecuteChanged();
        PreviewSelectedProfileCommand.NotifyCanExecuteChanged();
        ApplySelectedProfileCommand.NotifyCanExecuteChanged();
        CreateProfileCommand.NotifyCanExecuteChanged();
        DuplicateProfileCommand.NotifyCanExecuteChanged();
        RenameProfileCommand.NotifyCanExecuteChanged();
        DeleteProfileCommand.NotifyCanExecuteChanged();
        ExportProfileCommand.NotifyCanExecuteChanged();
        ImportProfileCommand.NotifyCanExecuteChanged();
        CopyProfileShareUriCommand.NotifyCanExecuteChanged();
        CopyProfileComparisonCommand.NotifyCanExecuteChanged();
    }

    private bool CanUseSelectedProfile() => !IsRunning && SelectedLocalProfile is not null;
    private bool CanCreateLocalProfile() => !IsRunning && !string.IsNullOrWhiteSpace(ProfileNameText);
    private bool CanRenameLocalProfile() => !IsRunning && SelectedLocalProfile?.IsEditable == true && !string.IsNullOrWhiteSpace(ProfileNameText);
    private bool CanDeleteLocalProfile() => !IsRunning && SelectedLocalProfile?.IsEditable == true;

    private async Task PreviewSelectedProfileAsync()
    {
        if (SelectedLocalProfile is null)
        {
            return;
        }

        var profile = await _profileService.LoadProfileAsync(SelectedLocalProfile.Id);
        ApplyConfigurationToEditor(profile.Configuration);
        ProfileOperationStatus = LF("Vm_ProfilePreviewedStatusFormat", profile.Summary.Name);
        AppendLog(LF("Vm_ProfilePreviewedLogFormat", profile.Summary.Name), "INFO");
    }

    private async Task ApplySelectedProfileAsync()
    {
        if (SelectedLocalProfile is null)
        {
            return;
        }

        var profile = await _profileService.LoadProfileAsync(SelectedLocalProfile.Id);
        ShowPrompt(
            LF("Vm_ProfileSetActiveTitleFormat", profile.Summary.Name),
            L("Vm_ProfileSetActiveBody"),
            L("Vm_ProfileSetActiveConfirm"),
            Strings.ButtonCancel,
            false,
            () => SetActiveProfileAsync(profile.Summary.Id),
            L("Vm_ProfilePreviewSummaryTitle"),
            BuildProfileSummary(profile.Configuration));
    }

    private async Task SetActiveProfileAsync(string id)
    {
        await _profileService.ApplyProfileAsync(id);
        var profile = await _profileService.LoadProfileAsync(id);
        ApplyConfigurationToEditor(profile.Configuration);
        await RefreshLocalProfilesAsync(profile.Summary.Id);
        await RefreshSnapshotAsync();
        ProfileOperationStatus = LF("Vm_ProfileActiveStatusFormat", profile.Summary.Name);
        AppendLog(LF("Vm_ProfileActiveLogFormat", profile.Summary.Name), "SUCCESS");
    }

    private async Task CreateLocalProfileAsync()
    {
        var profile = await _profileService.CreateFromConfigurationAsync(
            ProfileNameText,
            ProfileDescriptionText,
            BuildConfiguration("Custom"));
        await RefreshLocalProfilesAsync(profile.Summary.Id);
        ProfileOperationStatus = LF("Vm_ProfileSavedStatusFormat", profile.Summary.Name);
        AppendLog(LF("Vm_ProfileSavedLogFormat", profile.Summary.Name), "SUCCESS");
    }

    private async Task DuplicateLocalProfileAsync()
    {
        if (SelectedLocalProfile is null)
        {
            return;
        }

        var sourceName = SelectedLocalProfile.Name;
        var profile = await _profileService.DuplicateAsync(SelectedLocalProfile.Id, CreateCopyName(sourceName));
        await RefreshLocalProfilesAsync(profile.Summary.Id);
        ProfileOperationStatus = LF("Vm_ProfileDuplicatedStatusFormat", profile.Summary.Name, sourceName);
        AppendLog(LF("Vm_ProfileDuplicatedLogFormat", profile.Summary.Name), "SUCCESS");
    }

    private async Task RenameLocalProfileAsync()
    {
        if (SelectedLocalProfile is null)
        {
            return;
        }

        var profile = await _profileService.RenameAsync(SelectedLocalProfile.Id, ProfileNameText);
        await RefreshLocalProfilesAsync(profile.Summary.Id);
        ProfileOperationStatus = LF("Vm_ProfileRenamedStatusFormat", profile.Summary.Name);
        AppendLog(LF("Vm_ProfileRenamedLogFormat", profile.Summary.Name), "SUCCESS");
    }

    private Task DeleteLocalProfileAsync()
    {
        if (SelectedLocalProfile is null)
        {
            return Task.CompletedTask;
        }

        var profile = SelectedLocalProfile;
        ShowPrompt(
            LF("Vm_ProfileDeleteTitleFormat", profile.Name),
            L("Vm_ProfileDeleteBody"),
            L("Vm_ProfileDeleteConfirm"),
            L("Vm_ProfileDeleteCancel"),
            true,
            () => DeleteLocalProfileConfirmedAsync(profile.Id, profile.Name),
            L("Vm_ProfileDeleteSummaryTitle"),
            L("Vm_ProfileDeleteSummaryBody"));
        return Task.CompletedTask;
    }

    private async Task DeleteLocalProfileConfirmedAsync(string id, string name)
    {
        await _profileService.DeleteAsync(id);
        await RefreshLocalProfilesAsync();
        await RefreshSnapshotAsync();
        ProfileOperationStatus = LF("Vm_ProfileDeletedStatusFormat", name);
        AppendLog(LF("Vm_ProfileDeletedLogFormat", name), "WARN");
    }

    private async Task ExportLocalProfileAsync()
    {
        if (SelectedLocalProfile is null)
        {
            return;
        }

        var fileName = $"{SlugifyForFile(SelectedLocalProfile.Name)}.librespot";
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = L("Vm_ProfileExportDialogTitle"),
            Filter = L("Vm_ProfileExportDialogFilter"),
            DefaultExt = ".librespot",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = fileName
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await _profileService.ExportAsync(SelectedLocalProfile.Id, dialog.FileName);
        ProfileOperationStatus = LF("Vm_ProfileExportedStatusFormat", SelectedLocalProfile.Name, dialog.FileName);
        AppendLog(LF("Vm_ProfileExportedLogFormat", dialog.FileName), "SUCCESS");
    }

    private async Task RefreshSelectedProfileShareCardAsync()
    {
        var selected = SelectedLocalProfile;
        _selectedProfileShareCard = null;
        SelectedProfileQrImage = null;
        SelectedProfileShareStatus = selected is null
            ? L("Vm_ProfileShareInitial")
            : L("Vm_ProfileSharePreparing");
        SelectedProfileComparisonText = selected is null
            ? L("Vm_ProfileComparisonInitial")
            : L("Vm_ProfileComparisonPreparing");
        RaiseProfileShareCardStateChanged();

        if (selected is null)
        {
            return;
        }

        try
        {
            var shareCard = await _profileService.CreateShareCardAsync(selected.Id);
            var profile = await _profileService.LoadProfileAsync(selected.Id);
            if (SelectedLocalProfile?.Id != selected.Id)
            {
                return;
            }

            _selectedProfileShareCard = shareCard;
            SelectedProfileComparisonText = BuildProfileComparison(profile.Configuration);

            try
            {
                SelectedProfileQrImage = QrCodeImageService.CreateImage(shareCard.QrPayload);
                SelectedProfileShareStatus = L("Vm_ProfileShareReady");
            }
            catch (Exception ex)
            {
                SelectedProfileQrImage = null;
                SelectedProfileShareStatus = LF("Vm_ProfileShareQrTooLargeFormat", ex.Message);
            }

            RaiseProfileShareCardStateChanged();
        }
        catch (Exception ex)
        {
            // A slow load for a previously selected profile must not clobber
            // state for the profile the user has since selected.
            if (SelectedLocalProfile?.Id != selected.Id)
            {
                return;
            }

            SelectedProfileQrImage = null;
            SelectedProfileShareStatus = LF("Vm_ProfileShareFailedFormat", selected.Name, ex.Message);
            SelectedProfileComparisonText = L("Vm_ProfileComparisonUnavailable");
            RaiseProfileShareCardStateChanged();
        }
    }

    private void RaiseProfileShareCardStateChanged()
    {
        OnPropertyChanged(nameof(HasSelectedProfileShareCard));
        OnPropertyChanged(nameof(SelectedProfileShareUri));
        OnPropertyChanged(nameof(HasSelectedProfileQrImage));
        OnPropertyChanged(nameof(SelectedProfileShareStatus));
        OnPropertyChanged(nameof(SelectedProfileComparisonText));
        CopyProfileShareUriCommand.NotifyCanExecuteChanged();
        CopyProfileComparisonCommand.NotifyCanExecuteChanged();
    }

    private void CopyProfileShareUri()
    {
        if (_selectedProfileShareCard is null)
        {
            return;
        }

        TryCopyText(_selectedProfileShareCard.ShareUri, L("Vm_ProfileShareLinkCopied"), L("Vm_ProfileShareClipboardUnavailable"));
    }

    private void CopyProfileComparison()
    {
        if (SelectedLocalProfile is null)
        {
            return;
        }

        TryCopyText(SelectedProfileComparisonText, L("Vm_ProfileComparisonCopied"), L("Vm_ProfileComparisonClipboardUnavailable"));
    }

    private void TryCopyText(string text, string successMessage, string failureMessage)
    {
        ProfileOperationStatus = TrySetClipboardText(text) ? successMessage : failureMessage;
    }

    private string BuildProfileComparison(InstallConfiguration configuration)
    {
        var normalized = AppCatalog.NormalizeConfiguration(configuration);
        var recommended = AppCatalog.CreateRecommendedConfiguration();
        var changedAreas = new List<string>();

        if (!string.Equals(normalized.Spicetify_Theme, recommended.Spicetify_Theme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(normalized.Spicetify_Scheme, recommended.Spicetify_Scheme, StringComparison.OrdinalIgnoreCase))
        {
            changedAreas.Add(LF("Vm_ProfileComparisonAreaThemeFormat", normalized.Spicetify_Theme, normalized.Spicetify_Scheme));
        }

        if (!string.Equals(normalized.SpotX_LyricsTheme, recommended.SpotX_LyricsTheme, StringComparison.OrdinalIgnoreCase))
        {
            changedAreas.Add(LF("Vm_ProfileComparisonAreaLyricsFormat", Prettify.Label(normalized.SpotX_LyricsTheme)));
        }

        if (!SetEquals(normalized.Spicetify_Extensions, recommended.Spicetify_Extensions))
        {
            changedAreas.Add(LF("Vm_ProfileComparisonAreaExtensionsFormat", normalized.Spicetify_Extensions.Count));
        }

        if (normalized.Spicetify_CustomApps.Count > 0)
        {
            changedAreas.Add(LF("Vm_ProfileComparisonAreaCustomAppsFormat", normalized.Spicetify_CustomApps.Count));
        }

        if (normalized.SpotX_Premium != recommended.SpotX_Premium)
        {
            changedAreas.Add(L("Vm_ProfileComparisonAreaPremiumPatch"));
        }

        if (normalized.SpotX_CustomPatchesEnabled)
        {
            changedAreas.Add(L("Vm_ProfileComparisonAreaCustomPatches"));
        }

        if (normalized.CleanInstall != recommended.CleanInstall)
        {
            changedAreas.Add(normalized.CleanInstall ? L("Vm_ProfileComparisonAreaCleanInstall") : L("Vm_ProfileComparisonAreaOverlayInstall"));
        }

        var diffText = changedAreas.Count == 0
            ? L("Vm_ProfileComparisonMatchesBaseline")
            : LF("Vm_ProfileComparisonDiffersFormat", string.Join(", ", changedAreas));
        return LF(
            "Vm_ProfileComparisonSummaryFormat",
            normalized.Mode,
            diffText,
            normalized.Spicetify_Theme,
            normalized.Spicetify_Scheme,
            Prettify.Label(normalized.SpotX_LyricsTheme),
            normalized.Spicetify_Extensions.Count,
            normalized.Spicetify_CustomApps.Count,
            normalized.SpotX_CustomPatchesEnabled ? L("Vm_ToggleOn") : L("Vm_ToggleOff"));
    }

    private static bool SetEquals(IEnumerable<string> left, IEnumerable<string> right) =>
        left.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(right);

    private void OpenExternalUri(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true
            })?.Dispose();
        }
        catch (Exception ex)
        {
            ProfileOperationStatus = LF("Vm_OpenLinkFailedFormat", ex.Message);
        }
    }

    private async Task ImportLocalProfileAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = L("Vm_ProfileImportDialogTitle"),
            Filter = L("Vm_ProfileImportDialogFilter"),
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var preview = await _profileService.PreviewImportAsync(dialog.FileName);
        ShowPrompt(
            LF("Vm_ProfileImportTitleFormat", preview.Name),
            L("Vm_ProfileImportBody"),
            L("Vm_ProfileImportConfirm"),
            Strings.ButtonCancel,
            false,
            () => ImportLocalProfileConfirmedAsync(preview),
            L("Vm_ProfileImportedSettingsTitle"),
            BuildProfileSummary(preview.Configuration));
    }

    public async Task PreviewSharedProfileUriAsync(string shareUri)
    {
        var preview = await _profileService.PreviewShareUriAsync(shareUri);
        ShowPrompt(
            LF("Vm_ProfileImportSharedTitleFormat", preview.Name),
            L("Vm_ProfileImportSharedBody"),
            L("Vm_ProfileImportSharedConfirm"),
            Strings.ButtonCancel,
            false,
            () => ImportLocalProfileConfirmedAsync(preview),
            L("Vm_ProfileSharedSettingsTitle"),
            BuildProfileSummary(preview.Configuration));
    }

    private async Task ImportLocalProfileConfirmedAsync(LocalProfileImportPreview preview)
    {
        var profile = await _profileService.ImportAsync(preview);
        await RefreshLocalProfilesAsync(profile.Summary.Id);
        ProfileOperationStatus = LF("Vm_ProfileImportedStatusFormat", profile.Summary.Name);
        AppendLog(LF("Vm_ProfileImportedLogFormat", profile.Summary.Name), "SUCCESS");
    }

    private string CreateCopyName(string sourceName)
    {
        var baseName = string.IsNullOrWhiteSpace(sourceName) ? "Profile" : sourceName.Trim();
        if (!baseName.EndsWith(" Copy", StringComparison.OrdinalIgnoreCase))
        {
            baseName += " Copy";
        }

        var candidate = baseName;
        for (var suffix = 2; LocalProfiles.Any(profile => string.Equals(profile.Name, candidate, StringComparison.CurrentCultureIgnoreCase)); suffix++)
        {
            candidate = $"{baseName} {suffix}";
        }

        return candidate;
    }

    private static string SlugifyForFile(string value)
    {
        var safe = new string((value ?? "profile")
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());
        var compact = string.Join('-', safe.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(compact) ? "profile" : compact;
    }

    private string BuildProfileSummary(InstallConfiguration configuration)
    {
        var normalized = AppCatalog.NormalizeConfiguration(configuration);
        var extensionCount = normalized.Spicetify_Extensions.Count;
        var extensionText = extensionCount switch
        {
            0 => L("Vm_ProfileSummaryNoExtensions"),
            1 => L("Vm_ProfileSummaryOneExtension"),
            _ => LF("Vm_ProfileSummaryManyExtensionsFormat", extensionCount)
        };
        return LF(
            "Vm_ProfileSummaryFormat",
            normalized.Mode,
            normalized.Spicetify_Theme,
            normalized.Spicetify_Scheme,
            Prettify.Label(normalized.SpotX_LyricsTheme),
            extensionText,
            normalized.SpotX_Premium ? L("Vm_ToggleOn") : L("Vm_ToggleOff"),
            normalized.SpotX_CustomPatchesEnabled ? L("Vm_ToggleOn") : L("Vm_ToggleOff"));
    }

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
            ? $"{component.Evidence} Last changed: {component.LastChangedDisplay}."
            : component.Evidence;

        return new StatusDashboardItemViewModel(
            label,
            valueFactory(component),
            detail,
            component.Severity);
    }

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
            "SafeMode" => Snapshot.SpicetifyInstalled && HealthComponent("active-theme")?.Status != "Marketplace or stock",
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
            CurrentRun: currentRun);

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
            LogEntries.Select(entry => entry.CopyLine).ToArray());
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

    private void ValidateCustomPatches()
    {
        RefreshCustomPatchValidation();
        ShowNotice(
            _customPatchValidation.IsValid ? L("Vm_CustomPatchesDryRunPassed") : L("Vm_CustomPatchesNeedReviewTitle"),
            _customPatchValidation.Summary,
            _customPatchValidation.IsValid
                ? L("Vm_CustomPatchesDryRunPassedDetail")
                : string.Join(" ", _customPatchValidation.Errors.Take(2)));
    }

    private void FormatCustomPatches()
    {
        try
        {
            CustomPatchesJson = _customPatchService.Format(CustomPatchesJson);
            CustomPatchesEnabled = true;
            RefreshCustomPatchValidation();
            ShowNotice(
                L("Vm_CustomPatchesFormattedTitle"),
                L("Vm_CustomPatchesFormattedStatus"),
                L("Vm_CustomPatchesFormattedDetail"));
        }
        catch (JsonException ex)
        {
            ShowNotice(
                L("Vm_CustomPatchesNeedReviewTitle"),
                LF("Vm_CustomPatchesFormatFailedFormat", ex.Message),
                L("Vm_CustomPatchesFormatFailedDetail"));
        }
    }

    private void ClearCustomPatches()
    {
        CustomPatchesEnabled = false;
        CustomPatchesJson = string.Empty;
        CustomPatchesImportUrl = string.Empty;
        ClearCustomPatchProvenance();
        RefreshCustomPatchValidation();
    }

    private async Task ImportCustomPatchesFromUrlAsync()
    {
        var imported = await _customPatchService.ImportFromUrlAsync(CustomPatchesImportUrl);
        _preserveCustomPatchProvenance = true;
        try
        {
            CustomPatchesJson = _customPatchService.Format(imported.Json);
            SetCustomPatchProvenance(imported);
            CustomPatchesEnabled = true;
            RefreshCustomPatchValidation();
            ShowNotice(
                _customPatchValidation.IsValid ? L("Vm_CustomPatchesImportedTitle") : L("Vm_CustomPatchesImportedNeedReviewTitle"),
                _customPatchValidation.Summary,
                _customPatchValidation.IsValid
                    ? LF("Vm_CustomPatchesImportedDetailFormat", _customPatchesSourceSha256)
                    : string.Join(" ", _customPatchValidation.Errors.Take(2)));
        }
        finally
        {
            _preserveCustomPatchProvenance = false;
        }
    }

    private async Task ApplyRecommendedAsync()
    {
        if (!await EnsureRiskAcknowledgedAsync())
        {
            return;
        }

        var configuration = AppCatalog.CreateRecommendedConfiguration();
        configuration.Mode = "Easy";
        configuration.RiskAcknowledged = true;
        var planSummary = await CollectPlanSummaryAsync(configuration);
        ShowPrompt(
            L("Vm_RecommendedSetupPromptTitle"),
            planSummary,
            Strings.ButtonRunSetup,
            Strings.ButtonCancel,
            false,
            () => StartBackendRunAsync(
                "Install",
                configuration,
                L("Vm_RecommendedSetupActivityTitle"),
                L("Vm_RecommendedSetupActivityStatus"),
                0),
            L("Vm_PromptWhatThisWillDo"),
            L("Vm_SetupPromptSummaryBody"));
    }

    private async Task ApplyCustomAsync()
    {
        var configuration = BuildConfiguration("Custom");
        var customPatchValidation = _customPatchService.Validate(configuration.SpotX_CustomPatchesJson, configuration.SpotX_CustomPatchesEnabled);
        if (!customPatchValidation.IsValid)
        {
            _customPatchValidation = customPatchValidation;
            CustomPatchFindings.Clear();
            foreach (var finding in customPatchValidation.Findings)
            {
                CustomPatchFindings.Add(finding);
            }
            RaiseCustomPatchStateChanged();
            ShowNotice(
                L("Vm_CustomPatchesNeedReviewTitle"),
                customPatchValidation.Summary,
                string.Join(" ", customPatchValidation.Errors.Take(2)));
            return;
        }

        if (!await EnsureRiskAcknowledgedAsync())
        {
            return;
        }

        configuration.RiskAcknowledged = true;
        var planSummary = await CollectPlanSummaryAsync(configuration);
        ShowPrompt(
            L("Vm_CustomSetupPromptTitle"),
            planSummary,
            Strings.ButtonRunSetup,
            Strings.ButtonCancel,
            false,
            () => StartBackendRunAsync(
                "Install",
                configuration,
                L("Vm_CustomSetupActivityTitle"),
                L("Vm_CustomSetupActivityStatus"),
                1),
            L("Vm_PromptWhatThisWillDo"),
            L("Vm_SetupPromptSummaryBody"));
    }

    private async Task<string> CollectPlanSummaryAsync(InstallConfiguration configuration)
    {
        var planLines = new List<string>();
        // Plan is read-only, so the candidate configuration goes to a temp
        // file instead of config.json. The persistent save happens in
        // StartBackendRunAsync only after the user confirms the prompt â€”
        // cancelling the prompt must leave the previous config untouched,
        // because the auto-reapply watcher applies whatever config.json holds.
        var planConfigPath = Path.Combine(
            _configurationService.ConfigDirectory,
            $"config.plan.{Guid.NewGuid():N}.tmp.json");
        try
        {
            await _configurationService.SaveToPathAsync(configuration, planConfigPath);
            await _backendScriptService.RunAsync("Plan", planConfigPath, message =>
            {
                if (message.Kind == "plan")
                {
                    try
                    {
                        var entry = System.Text.Json.JsonDocument.Parse(message.Payload);
                        var desc = entry.RootElement.GetProperty("description").GetString() ?? "";
                        var wouldChange = entry.RootElement.GetProperty("wouldChange").GetBoolean();
                        if (wouldChange && !string.IsNullOrWhiteSpace(desc))
                        {
                            planLines.Add(desc);
                        }
                    }
                    catch { }
                }
            });
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            System.Diagnostics.Debug.WriteLine($"Plan summary collection failed: {ex.Message}");
        }
        finally
        {
            try { File.Delete(planConfigPath); } catch { }
        }

        var compatWarnings = AppCatalog.CheckInstalledSpotifyCompatibility(
            Snapshot.HealthReport.Components
                .FirstOrDefault(c => string.Equals(c.Id, "spotify", StringComparison.OrdinalIgnoreCase))
                ?.DetectedVersion);

        if (planLines.Count == 0 && compatWarnings.Count == 0)
        {
            return L("Vm_PlanSummaryDefault");
        }

        var sb = new System.Text.StringBuilder();

        if (compatWarnings.Count > 0)
        {
            sb.AppendLine(L("Vm_PlanSummaryCompatibilityWarning"));
            foreach (var warning in compatWarnings)
            {
                sb.AppendLine(warning);
            }
            sb.AppendLine();
        }

        if (planLines.Count > 0)
        {
            sb.AppendLine(L("Vm_PlanSummaryStepsTitle"));
            sb.AppendLine();
            foreach (var line in planLines)
            {
                sb.Append("- ");
                sb.AppendLine(line);
            }
        }

        return sb.ToString().TrimEnd();
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

        var body = definition.Action == "RemoveSelfData"
            ? $"{definition.Description}{Environment.NewLine}{Environment.NewLine}{L("Vm_MaintenanceRemoveSelfDataBodySuffix")}"
            : definition.IsDestructive
                ? $"{definition.Description}{Environment.NewLine}{Environment.NewLine}{L("Vm_MaintenanceDestructiveBodySuffix")}"
                : $"{definition.Description}{Environment.NewLine}{Environment.NewLine}{L("Vm_MaintenanceStandardBodySuffix")}";
        var (summaryTitle, summaryBody) = BuildMaintenancePromptSummary(definition);
        var requiresAdministrator = RequiresAdministrator(definition.Action);

        ShowPrompt(
            definition.Title,
            body,
            definition.ButtonText,
            definition.IsDestructive ? L("Vm_KeepCurrentSetup") : Strings.ButtonCancel,
            definition.IsDestructive,
            () => StartBackendRunAsync(definition.Action, null, definition.Title, definition.Description, 2, requiresAdministrator),
            summaryTitle,
            summaryBody);
    }

    private static bool RequiresAdministrator(string action) => false;

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
            () => StartBackendRunAsync(action, null, title, status, 2, requiresAdministrator: false),
            L("Vm_PromptWhatThisDoes"),
            summaryBody);
    }

    private async Task StartBackendRunAsync(
        string action,
        InstallConfiguration? configuration,
        string title,
        string status,
        int targetWorkspaceIndex,
        bool requiresAdministrator = true)
    {
        // Critical: flip IsRunning synchronously *before* any await so the
        // Apply button's CanExecute immediately returns false. Without this
        // a rapid double-click queues two concurrent backend runs.
        if (IsRunning)
        {
            return;
        }

        if (requiresAdministrator && !IsAdministratorSession)
        {
            // Stage the confirmed setup so the elevated relaunch can resume it
            // without a second click. The user already confirmed the plan prompt
            // before reaching here, so persisting the config now is safe.
            var canResume = false;
            if (configuration is not null && string.Equals(action, "Install", StringComparison.Ordinal))
            {
                try
                {
                    await _configurationService.SaveAsync(configuration, CancellationToken.None);
                    canResume = true;
                }
                catch (Exception ex)
                {
                    AppendLog(LF("Vm_LogConfigStageFailed", ex.Message), "WARN");
                }
            }

            PresentAdministratorPrompt(resumeInstall: canResume);
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
        _activityState.Begin(title, status, Strings.PreparingBackend);
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

            var result = await _backendScriptService.RunAsync(action, _configurationService.ConfigPath, HandleBackendMessage, token);
            _lastBackendRunResult = result;
            if (result.Canceled)
            {
                AppendLog(result.ErrorMessage ?? L("Vm_LogBackendCanceled"), "WARN");
                _activityOutcome = ActivityOutcome.Canceled;
                ActivityStatus = Strings.Canceled;
            }
            else if (!result.Success)
            {
                AppendLog(result.ErrorMessage ?? "LibreSpot reported an unknown backend failure.", "ERROR");
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
            await RefreshSnapshotAsync();
            if (runSucceeded)
            {
                RefreshUndoActionItems();
            }
        }

        if (ExitAfterSuccessfulSetup && runSucceeded && ShouldExitAfterSuccessfulRun(action, configuration))
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
        ShouldRestartSpotifyAfterSuccessfulRun(action, configuration);

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

        AppendLog(result.Message, result.Reopened ? "INFO" : "WARN");
        ActivityStatus = Strings.RunComplete;
        ActivityStep = result.Reopened ? L("Vm_SpotifyReopened") : L("Vm_SpotifyRestartSkipped");
        ProgressValue = 100;
    }

    private static bool ShouldRestartSpotifyAfterSuccessfulRun(string action, InstallConfiguration? configuration) =>
        action switch
        {
            "Install" => configuration?.LaunchAfter ?? true,
            "Reapply" or "RepairMarketplace" or "SafeMode" or "RestoreBackup" or "RestoreVanilla" => true,
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

    private void HandleBackendMessage(BackendMessage message)
    {
        // Use BeginInvoke (fire-and-forget) instead of synchronous Invoke to
        // prevent deadlock during shutdown: if the dispatcher thread is blocked
        // waiting for the backend process to exit while the process output
        // callback tries to Invoke back onto the dispatcher, both threads block.
        _dispatcher.BeginInvoke(() =>
        {
            switch (message.Kind)
            {
                case "progress":
                    if (double.TryParse(message.Payload, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var value))
                    {
                        ProgressValue = Math.Clamp(value, 0, 100);
                    }
                    break;
                case "status":
                    ActivityStatus = message.Payload;
                    break;
                case "step":
                    ActivityStep = message.Payload;
                    break;
                case "result":
                    if (string.Equals(message.Level, "SUCCESS", StringComparison.OrdinalIgnoreCase))
                    {
                        _activityOutcome = ActivityOutcome.Success;
                        ActivityStatus = Strings.RunComplete;
                        ActivityStep = L("Vm_LibreSpotReady");
                        ProgressValue = 100;
                    }
                    else
                    {
                        _activityOutcome = ActivityOutcome.Error;
                        ActivityStatus = Strings.RunNeedsAttention;
                    }

                    AppendLog(message.Payload, message.Level);
                    break;
                default:
                    AppendLog(message.Payload, message.Level);
                    break;
            }
        });
    }

    private void AppendLog(string payload, string level)
    {
        _activityState.AppendLog(payload, level, DateTime.Now);
    }

    private void ClearLog() => _activityState.ClearLog();

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

    private async Task RefreshSnapshotAsync()
    {
        SetSnapshotQueryState(isLoading: true, loadFailed: false);
        try
        {
            _environmentState.Update(
                await _snapshotService.GetSnapshotAsync(_configurationService.ConfigPath),
                DateTime.Now);
            SetSnapshotQueryState(isLoading: false, loadFailed: false);
            RaiseSnapshotInsightsChanged();
        }
        catch (Exception ex)
        {
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

    /// <summary>
    /// Startup hook for the <c>--shell-action=resume-install</c> relaunch: runs
    /// the setup the standard-mode session staged, so an elevate-and-relaunch
    /// finishes what the user already confirmed instead of dropping them back at
    /// the normal setup entry point. Only proceeds when actually elevated and
    /// the staged config was risk-acknowledged, so it never auto-runs a mutating
    /// operation the user did not confirm.
    /// </summary>
    public async Task ResumeElevatedInstallAsync()
    {
        if (!IsAdministratorSession)
        {
            SelectedWorkspaceIndex = 0;
            return;
        }

        InstallConfiguration configuration;
        try
        {
            configuration = await _configurationService.LoadAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            AppendLog(LF("Vm_LogElevatedResumeFailed", ex.Message), "WARN");
            SelectedWorkspaceIndex = 0;
            return;
        }

        if (!configuration.RiskAcknowledged)
        {
            // No confirmed setup to resume — fall back to the normal landing view.
            SelectedWorkspaceIndex = 0;
            return;
        }

        AppendLog(L("Vm_LogResumingElevatedSetup"), "INFO");
        await StartBackendRunAsync(
            "Install",
            configuration,
            L("Vm_ElevatedResumeActivityTitle"),
            L("Vm_ElevatedResumeActivityStatus"),
            configuration.Mode == "Custom" ? 1 : 0);
    }

    private void PresentAdministratorPrompt(bool resumeInstall = false)
    {
        _resumeInstallAfterElevation = resumeInstall;
        ShowPrompt(
            L("Vm_AdminPromptTitle"),
            LF("Vm_AdminPromptBodyFormat", Environment.NewLine + Environment.NewLine),
            L("Ui_RelaunchAsAdministrator"),
            L("Vm_AdminPromptKeepReviewing"),
            false,
            () =>
            {
                RelaunchAsAdministrator();
                return Task.CompletedTask;
            },
            L("Vm_AdminPromptSummaryTitle"),
            L("Vm_AdminPromptSummaryBody"));
    }

    private void RelaunchAsAdministrator()
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                ShowNotice(L("Vm_RelaunchUnableTitle"), L("Vm_RelaunchMissingPathStatus"), L("Vm_RelaunchStayStandard"));
                return;
            }

            // When running via `dotnet run`, ProcessPath points at dotnet.exe. Relaunching
            // that as admin would not start LibreSpot â€” warn instead of confusing the user.
            var exeName = Path.GetFileName(executablePath);
            if (string.Equals(exeName, "dotnet.exe", StringComparison.OrdinalIgnoreCase))
            {
                ShowNotice(
                    L("Vm_RelaunchDeveloperTitle"),
                    L("Vm_RelaunchDeveloperStatus"),
                    L("Vm_RelaunchDeveloperStep"));
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = true,
                Verb = "runas"
            };
            if (_resumeInstallAfterElevation)
            {
                // Tell the elevated instance to resume the staged setup on
                // startup, removing the second "Run setup" click.
                startInfo.ArgumentList.Add("--shell-action=resume-install");
            }

            var process = Process.Start(startInfo);
            if (process is null)
            {
                ShowNotice(L("Vm_RelaunchFailedTitle"), L("Vm_RelaunchFailedStatus"), L("Vm_RelaunchStayStandard"));
                return;
            }
            process.Dispose();
            Application.Current.Shutdown();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // 1223 = ERROR_CANCELLED. User clicked "No" on the UAC prompt â€” not an error.
            ShowNotice(
                L("Vm_RelaunchCanceledTitle"),
                L("Vm_RelaunchCanceledStatus"),
                L("Vm_RelaunchWaitingStep"));
        }
        catch (Exception ex)
        {
            AppendLog(ex.Message, "WARN");
            ShowNotice(
                L("Vm_RelaunchUnableTitle"),
                L("Vm_RelaunchExceptionStatus"),
                L("Vm_RelaunchElevationFailedStep"));
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

    private void ShowNotice(string title, string status, string step)
    {
        _runStopwatch.Reset();
        _runElapsedTimer.Stop();
        _activityState.ShowNotice(title, status, step);
        OnPropertyChanged(nameof(RunElapsedText));
    }

    public void ApplyUiAutomationSmokeState(string state)
    {
        var normalizedState = state.Trim().ToLowerInvariant();
        if (normalizedState is "recommended" or "custom" or "maintenance")
        {
            SeedUiAutomationActivityLog();
        }

        switch (normalizedState)
        {
            case "custom":
                SelectedWorkspaceIndex = 1;
                break;
            case "maintenance":
                SelectedWorkspaceIndex = 2;
                break;
            case "activity-empty":
                SelectedWorkspaceIndex = 0;
                ClearLog();
                break;
            case "snapshot-error":
                SelectedWorkspaceIndex = 0;
                ClearLog();
                SetSnapshotQueryState(isLoading: false, loadFailed: true);
                break;
            case "custom-no-results":
                SelectedWorkspaceIndex = 1;
                SettingsSearchText = "__no_matching_setting__";
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
                    Strings.ProgressSpotifyReady);
                ProgressValue = 100;
                break;
            case "activity-running":
                SelectedWorkspaceIndex = 0;
                _activityOutcome = ActivityOutcome.None;
                _activityState.Begin(
                    Strings.ActivityDialogName,
                    Strings.StatusInProgress,
                    Strings.PreparingBackend);
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
                    "Backend reported an error");
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
                AppendLog("UI automation smoke activity with reversible changes.", "INFO");
                ShowNotice(
                    Strings.ActivityDialogName,
                    Strings.RunComplete,
                    Strings.ProgressSpotifyReady);
                ProgressValue = 100;
                break;
            default:
                SelectedWorkspaceIndex = 0;
                break;
        }
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
        AppendLog(ShellReadinessDetail, NeedsAdministratorRelaunch ? "WARN" : "INFO");
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

    private void ApplyConfigurationToEditor(InstallConfiguration configuration)
    {
        configuration = AppCatalog.NormalizeConfiguration(configuration);

        ApplyOptionValues(InstallOptions, configuration);
        ApplyOptionValues(CoreOptions, configuration);
        ApplyOptionValues(InterfaceOptions, configuration);
        ApplyOptionValues(AdvancedOptions, configuration);
        ApplyOptionValues(ExperienceOptions, configuration);

        SelectedTheme = AppCatalog.ThemeSchemes.ContainsKey(configuration.Spicetify_Theme)
            ? configuration.Spicetify_Theme
            : "(None - Marketplace Only)";
        SelectedLyricsTheme = AppCatalog.LyricsThemes.Contains(configuration.SpotX_LyricsTheme)
            ? configuration.SpotX_LyricsTheme
            : "spotify";
        SelectedSpotifyVersionId = AppCatalog.SpotifyVersionManifest.Any(entry => string.Equals(entry.Id, configuration.SpotX_SpotifyVersionId, StringComparison.Ordinal))
            ? configuration.SpotX_SpotifyVersionId
            : "auto";
        SelectedDownloadMethod = AppCatalog.DownloadMethods.Any(entry => string.Equals(entry.Id, configuration.SpotX_DownloadMethod, StringComparison.Ordinal))
            ? configuration.SpotX_DownloadMethod
            : string.Empty;
        CacheLimitText = configuration.SpotX_CacheLimit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var themeSchemes = AppCatalog.ThemeSchemes.TryGetValue(SelectedTheme, out var s) ? s : Array.Empty<string>();
        SelectedScheme = themeSchemes.Contains(configuration.Spicetify_Scheme)
            ? configuration.Spicetify_Scheme
            : themeSchemes.FirstOrDefault() ?? "Default";

        var extensionLookup = configuration.Spicetify_Extensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in Extensions)
        {
            extension.IsSelected = extensionLookup.Contains(extension.Key);
        }

        var customAppLookup = configuration.Spicetify_CustomApps.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var customApp in CustomApps)
        {
            customApp.IsSelected = customAppLookup.Contains(customApp.Key);
        }

        CustomPatchesEnabled = configuration.SpotX_CustomPatchesEnabled;
        _preserveCustomPatchProvenance = true;
        try
        {
            CustomPatchesJson = configuration.SpotX_CustomPatchesJson;
            SetCustomPatchProvenance(configuration);
        }
        finally
        {
            _preserveCustomPatchProvenance = false;
        }

        RaiseSelectionInsightsChanged();
    }

    private static void ApplyOptionValues(IEnumerable<OptionToggleViewModel> options, InstallConfiguration configuration)
    {
        foreach (var option in options)
        {
            var property = typeof(InstallConfiguration).GetProperty(option.Key, BindingFlags.Public | BindingFlags.Instance);
            option.IsSelected = property?.GetValue(configuration) is bool value && value;
        }
    }

    private InstallConfiguration BuildConfiguration(string mode)
    {
        var configuration = AppCatalog.CreateRecommendedConfiguration();
        configuration.Mode = mode;
        configuration.UiCulture = SelectedLocalizationOption.CultureName;

        ApplyOptionsToConfiguration(InstallOptions, configuration);
        ApplyOptionsToConfiguration(CoreOptions, configuration);
        ApplyOptionsToConfiguration(InterfaceOptions, configuration);
        ApplyOptionsToConfiguration(AdvancedOptions, configuration);
        ApplyOptionsToConfiguration(ExperienceOptions, configuration);

        configuration.SpotX_LyricsTheme = SelectedLyricsTheme;
        configuration.SpotX_SpotifyVersionId = SelectedSpotifyVersionId;
        configuration.SpotX_DownloadMethod = SelectedDownloadMethod;
        configuration.Spicetify_Theme = SelectedTheme;
        configuration.Spicetify_Scheme = SelectedScheme;
        configuration.SpotX_CacheLimit = int.TryParse(
            CacheLimitText,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? Math.Clamp(parsed, 0, 50_000) // match Backend.ps1 upper bound
            : 0;
        configuration.SpotX_CustomPatchesEnabled = CustomPatchesEnabled;
        configuration.SpotX_CustomPatchesJson = CustomPatchesJson;
        configuration.SpotX_CustomPatchesSourceUrl = _customPatchesSourceUrl;
        configuration.SpotX_CustomPatchesFetchedAtUtc = _customPatchesFetchedAtUtc;
        configuration.SpotX_CustomPatchesSourceByteCount = _customPatchesSourceByteCount;
        configuration.SpotX_CustomPatchesSourceSha256 = _customPatchesSourceSha256;
        configuration.Spicetify_Extensions = Extensions.Where(item => item.IsSelected).Select(item => item.Key).ToList();
        configuration.Spicetify_CustomApps = CustomApps.Where(item => item.IsSelected).Select(item => item.Key).ToList();

        return AppCatalog.NormalizeConfiguration(configuration);
    }

    private void SetCustomPatchProvenance(CustomPatchImportResult imported)
    {
        _customPatchesSourceUrl = imported.SourceUrl;
        _customPatchesFetchedAtUtc = imported.FetchedAtUtc;
        _customPatchesSourceByteCount = imported.ByteCount;
        _customPatchesSourceSha256 = imported.Sha256;
        RaiseCustomPatchProvenanceChanged();
    }

    private void SetCustomPatchProvenance(InstallConfiguration configuration)
    {
        _customPatchesSourceUrl = configuration.SpotX_CustomPatchesSourceUrl;
        _customPatchesFetchedAtUtc = configuration.SpotX_CustomPatchesFetchedAtUtc;
        _customPatchesSourceByteCount = configuration.SpotX_CustomPatchesSourceByteCount;
        _customPatchesSourceSha256 = configuration.SpotX_CustomPatchesSourceSha256;
        RaiseCustomPatchProvenanceChanged();
    }

    private void ClearCustomPatchProvenance()
    {
        if (string.IsNullOrEmpty(_customPatchesSourceUrl) &&
            _customPatchesFetchedAtUtc is null &&
            _customPatchesSourceByteCount == 0 &&
            string.IsNullOrEmpty(_customPatchesSourceSha256))
        {
            return;
        }

        _customPatchesSourceUrl = string.Empty;
        _customPatchesFetchedAtUtc = null;
        _customPatchesSourceByteCount = 0;
        _customPatchesSourceSha256 = string.Empty;
        RaiseCustomPatchProvenanceChanged();
    }

    private void RaiseCustomPatchProvenanceChanged()
    {
        OnPropertyChanged(nameof(HasCustomPatchImportProvenance));
        OnPropertyChanged(nameof(CustomPatchesImportProvenance));
    }

    private static void ApplyOptionsToConfiguration(IEnumerable<OptionToggleViewModel> options, InstallConfiguration configuration)
    {
        foreach (var option in options)
        {
            var property = typeof(InstallConfiguration).GetProperty(option.Key, BindingFlags.Public | BindingFlags.Instance);
            property?.SetValue(configuration, option.IsSelected);
        }
    }

    private void RebuildSelectionInsights()
    {
        SelectionInsights.Clear();
        SelectedExtensionLabels.Clear();

        foreach (var extension in Extensions.Where(item => item.IsSelected))
        {
            SelectedExtensionLabels.Add(extension.Title);
        }

        var selectedCustomApps = CustomApps.Where(item => item.IsSelected).Select(item => item.Title).ToArray();

        var advancedCount = AdvancedOptions.Count(option => option.IsSelected);

        if (advancedCount == 0)
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "accent",
                L("Vm_InsightConservativeCoreTitle"),
                L("Vm_InsightConservativeCoreDetail")));
        }
        else if (advancedCount <= 2)
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "accent",
                L("Vm_InsightBalancedCustomizationTitle"),
                L("Vm_InsightBalancedCustomizationDetail")));
        }
        else
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "warning",
                L("Vm_InsightExperimentalTerritoryTitle"),
                L("Vm_InsightExperimentalTerritoryDetail")));
        }

        if (HasConflictingSidebarOptions())
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "warning",
                L("Vm_InsightRightSidebarOverlapTitle"),
                L("Vm_InsightRightSidebarOverlapDetail")));
        }
        else if (!IsOptionSelected(nameof(InstallConfiguration.CleanInstall)) && Snapshot.SpotifyInstalled)
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "warning",
                L("Vm_InsightOverlayInstallTitle"),
                L("Vm_InsightOverlayInstallDetail")));
        }
        else if (!IsOptionSelected(nameof(InstallConfiguration.CleanInstall)))
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "warning",
                L("Vm_InsightSkippingCleanStartTitle"),
                L("Vm_InsightSkippingCleanStartDetail")));
        }
        else
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "muted",
                L("Vm_InsightFreshBaselineTitle"),
                L("Vm_InsightFreshBaselineDetail")));
        }

        if (!IsLyricsThemeAvailable)
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "muted",
                L("Vm_InsightLyricsStylingParkedTitle"),
                L("Vm_InsightLyricsStylingParkedDetail")));
        }
        else if (!IsThemeSchemeAvailable)
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "muted",
                L("Vm_InsightMarketplaceFirstTitle"),
                L("Vm_InsightMarketplaceFirstDetail")));
        }
        else
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "accent",
                L("Vm_InsightThemeRestoreReadyTitle"),
                LF("Vm_InsightThemeRestoreReadyDetailFormat", SelectedTheme, Prettify.Label(SelectedScheme))));
        }

        if (!IsOptionSelected(nameof(InstallConfiguration.Spicetify_Marketplace)) && SelectedExtensionLabels.Count == 0 && !IsThemeSchemeAvailable)
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "warning",
                L("Vm_InsightMinimalSpicetifyTitle"),
                L("Vm_InsightMinimalSpicetifyDetail")));
        }

        if (!string.Equals(SelectedSpotifyVersionId, "auto", StringComparison.Ordinal))
        {
            var versionTone =
                SelectedSpotifyVersionId.Contains("win7", StringComparison.OrdinalIgnoreCase) ||
                SelectedSpotifyVersionId.Contains(".x86", StringComparison.OrdinalIgnoreCase)
                    ? "warning"
                    : "accent";

            SelectionInsights.Add(new SelectionInsightViewModel(
                versionTone,
                L("Vm_InsightPinnedCompatibilityTitle"),
                CurrentSpotifyVersionEntry.Notes));
        }

        if (HasArchitectureMismatch)
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "warning",
                L("Vm_InsightArchitectureMismatchTitle"),
                ArchitectureMismatchWarning!));
        }

        if (!string.IsNullOrWhiteSpace(SelectedDownloadMethod))
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "muted",
                CurrentDownloadMethodEntry.Label,
                CurrentDownloadMethodEntry.Detail));
        }

        if (CustomPatchesEnabled)
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                _customPatchValidation.IsValid ? "accent" : "warning",
                _customPatchValidation.IsValid ? L("Vm_InsightCustomPatchesReadyTitle") : L("Vm_InsightCustomPatchesReviewTitle"),
                CustomPatchesSummary));
        }

        if (selectedCustomApps.Length > 0)
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "info",
                L("Vm_InsightCustomAppsTitle"),
                LF("Vm_InsightCustomAppsDetailFormat", string.Join(", ", selectedCustomApps))));
        }
    }

    private int CountProfileDifferencesFromRecommended()
    {
        var differences = EnumerateAllOptions().Count(option => option.IsSelected != option.IsRecommendedDefault);
        differences += Extensions.Count(extension => extension.IsSelected != extension.IsRecommendedDefault);
        differences += CustomApps.Count(customApp => customApp.IsSelected != customApp.IsRecommendedDefault);

        if (!string.Equals(SelectedTheme, _recommendedBaseline.Spicetify_Theme, StringComparison.Ordinal))
        {
            differences++;
        }

        if (!string.Equals(SelectedScheme, _recommendedBaseline.Spicetify_Scheme, StringComparison.Ordinal))
        {
            differences++;
        }

        if (!string.Equals(SelectedLyricsTheme, _recommendedBaseline.SpotX_LyricsTheme, StringComparison.Ordinal))
        {
            differences++;
        }

        var cacheLimit = int.TryParse(
            CacheLimitText,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? Math.Clamp(parsed, 0, 50_000)
            : 0;

        if (cacheLimit != _recommendedBaseline.SpotX_CacheLimit)
        {
            differences++;
        }

        if (!string.Equals(SelectedSpotifyVersionId, _recommendedBaseline.SpotX_SpotifyVersionId, StringComparison.Ordinal))
        {
            differences++;
        }

        if (!string.Equals(SelectedDownloadMethod, _recommendedBaseline.SpotX_DownloadMethod, StringComparison.Ordinal))
        {
            differences++;
        }

        if (CustomPatchesEnabled != _recommendedBaseline.SpotX_CustomPatchesEnabled)
        {
            differences++;
        }

        if (!string.IsNullOrWhiteSpace(CustomPatchesJson))
        {
            differences++;
        }

        return differences;
    }

    private OptionToggleViewModel? FindOption(string key) =>
        EnumerateAllOptions().FirstOrDefault(option => string.Equals(option.Key, key, StringComparison.Ordinal));

    private bool IsOptionSelected(string key) =>
        FindOption(key)?.IsSelected == true;

    private static bool IsChangedOption(OptionToggleViewModel? option, string key) =>
        option is not null && string.Equals(option.Key, key, StringComparison.Ordinal);

    private bool HasConflictingSidebarOptions() =>
        IsOptionSelected(nameof(InstallConfiguration.SpotX_RightSidebarOff)) &&
        IsOptionSelected(nameof(InstallConfiguration.SpotX_RightSidebarClr));

    private enum ActivityOutcome { None, Success, Error, Canceled }
}
