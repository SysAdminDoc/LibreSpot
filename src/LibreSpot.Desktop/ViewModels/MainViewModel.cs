using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;

namespace LibreSpot.Desktop.ViewModels;

public sealed class OptionToggleViewModel : ObservableObject
{
    private bool _isSelected;

    public OptionToggleViewModel(string key, string title, string description, bool isRecommendedDefault)
    {
        Key = key;
        Title = title;
        Description = description;
        IsRecommendedDefault = isRecommendedDefault;
    }

    public string Key { get; }
    public string Title { get; }
    public string Description { get; }
    public bool IsRecommendedDefault { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class ExtensionToggleViewModel : ObservableObject
{
    private bool _isSelected;

    public ExtensionToggleViewModel(string key, string title, string description, bool isRecommendedDefault)
    {
        Key = key;
        Title = title;
        Description = description;
        IsRecommendedDefault = isRecommendedDefault;
    }

    public string Key { get; }
    public string Title { get; }
    public string Description { get; }
    public bool IsRecommendedDefault { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class LogEntryViewModel
{
    public LogEntryViewModel(DateTime timestamp, string level, string message)
    {
        Timestamp = timestamp;
        Level = string.IsNullOrWhiteSpace(level) ? "INFO" : level.Trim().ToUpperInvariant();
        Message = message;
    }

    public DateTime Timestamp { get; }
    public string Level { get; }
    public string Message { get; }

    public string TimestampDisplay => Timestamp.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

    public string CopyLine => $"[{TimestampDisplay}] [{Level}] {Message}";
}

public sealed class MaintenanceActionCardViewModel : ObservableObject
{
    private bool _isRelevant = true;

    public MaintenanceActionCardViewModel(
        MaintenanceActionDefinition definition,
        Func<MaintenanceActionDefinition, Task> runAsync,
        Func<bool> canRun,
        Action<Exception> onException)
    {
        Definition = definition;
        Command = new AsyncRelayCommand(
            () => runAsync(Definition),
            () => IsRelevant && canRun(),
            onException);
    }

    public MaintenanceActionDefinition Definition { get; }
    public string Action => Definition.Action;
    public string Title => Definition.Title;
    public string Description => Definition.Description;
    public string ButtonText => Definition.ButtonText;
    public bool IsDestructive => Definition.IsDestructive;
    public AsyncRelayCommand Command { get; }

    public bool IsRelevant
    {
        get => _isRelevant;
        private set
        {
            if (SetProperty(ref _isRelevant, value))
            {
                Command.RaiseCanExecuteChanged();
            }
        }
    }

    public void RefreshRelevance(bool isRelevant)
    {
        IsRelevant = isRelevant;
        Command.RaiseCanExecuteChanged();
    }
}

public sealed class SupportBundleCategoryViewModel : ObservableObject
{
    private readonly Action _selectionChanged;
    private bool _isRefreshing;
    private bool _isSelected;
    private string _detail;
    private string _fileCountText = "0 files";
    private string _estimatedSizeText = "0 B";

    public SupportBundleCategoryViewModel(
        string id,
        string title,
        bool isRequired,
        bool isSelected,
        string detail,
        Action selectionChanged)
    {
        Id = id;
        Title = title;
        IsRequired = isRequired;
        _isSelected = isRequired || isSelected;
        _detail = detail;
        _selectionChanged = selectionChanged;
    }

    public string Id { get; }
    public string Title { get; }
    public bool IsRequired { get; }
    public bool IsOptional => !IsRequired;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            var next = IsRequired || value;
            if (SetProperty(ref _isSelected, next) && !_isRefreshing)
            {
                _selectionChanged();
            }
        }
    }

    public string Detail
    {
        get => _detail;
        private set => SetProperty(ref _detail, value);
    }

    public string FileCountText
    {
        get => _fileCountText;
        private set => SetProperty(ref _fileCountText, value);
    }

    public string EstimatedSizeText
    {
        get => _estimatedSizeText;
        private set => SetProperty(ref _estimatedSizeText, value);
    }

    public void Refresh(SupportBundlePreviewEntry entry)
    {
        _isRefreshing = true;
        try
        {
            Detail = entry.Detail;
            FileCountText = entry.FileCount == 1 ? "1 file" : $"{entry.FileCount} files";
            EstimatedSizeText = MainViewModel.FormatBytes(entry.EstimatedBytes);
            IsSelected = entry.IsSelected;
        }
        finally
        {
            _isRefreshing = false;
        }
    }
}

public sealed class SelectionInsightViewModel
{
    public SelectionInsightViewModel(string tone, string title, string detail)
    {
        Tone = tone;
        Title = title;
        Detail = detail;
    }

    public string Tone { get; }
    public string Title { get; }
    public string Detail { get; }
}

public sealed class MainViewModel : ObservableObject, IDisposable
{
    // Cap the live log so a very chatty backend run can't pin UI memory or make
    // the ItemsControl render pathologically slow. Extra lines are still copied
    // via the `install.log` file the backend maintains.
    private const int MaxLogEntries = 2000;

    private readonly ConfigurationService _configurationService;
    private readonly BackendScriptService _backendScriptService;
    private readonly EnvironmentSnapshotService _snapshotService;
    private readonly SupportBundleService _supportBundleService;
    private readonly Dispatcher _dispatcher;
    private readonly bool _isAdministratorSession;
    private readonly InstallConfiguration _recommendedBaseline;
    private readonly List<MaintenanceActionCardViewModel> _maintenanceCards;
    private readonly Stopwatch _runStopwatch = new();
    private readonly DispatcherTimer _runElapsedTimer;
    private readonly DispatcherTimer _snapshotFreshnessTimer;
    private CancellationTokenSource? _runCts;

    private string _selectedTheme = "(None - Marketplace Only)";
    private string _selectedScheme = "Default";
    private string _selectedLyricsTheme = "spotify";
    private string _selectedSpotifyVersionId = "auto";
    private string _selectedDownloadMethod = string.Empty;
    private string _cacheLimitText = "0";
    private string _settingsSearchText = string.Empty;
    private int _selectedWorkspaceIndex;
    private bool _isActivityVisible;
    private bool _isRunning;
    private double _progressValue;
    private string _activityTitle = "Ready when you are";
    private string _activityStatus = "Pick a setup path to begin.";
    private string _activityStep = "Idle";
    private DateTime? _snapshotRefreshedAt;
    private EnvironmentSnapshot _snapshot = new();
    private bool _isPromptVisible;
    private string _promptTitle = string.Empty;
    private string _promptBody = string.Empty;
    private string _promptConfirmText = "Continue";
    private string _promptCancelText = "Cancel";
    private string _promptSummaryTitle = string.Empty;
    private string _promptSummaryBody = string.Empty;
    private bool _isPromptDestructive;
    private bool _isCancelRequested;
    private bool _isApplyingSelectionDependencyRules;
    private ConfigurationLoadState _configurationLoadState = ConfigurationLoadState.Loaded;
    private string? _recoveredConfigurationPath;
    private string? _configurationRecoveryReason;
    private Func<Task>? _pendingPromptAction;
    private SupportBundlePreview _supportBundlePreview = new(
        Array.Empty<SupportBundlePreviewEntry>(),
        0,
        Array.Empty<string>());
    private string _supportBundleLastExportText = "No support bundle exported in this session.";

    public MainViewModel(
        ConfigurationService configurationService,
        BackendScriptService backendScriptService,
        EnvironmentSnapshotService snapshotService,
        SupportBundleService? supportBundleService = null)
    {
        _configurationService = configurationService;
        _backendScriptService = backendScriptService;
        _snapshotService = snapshotService;
        _supportBundleService = supportBundleService ?? new SupportBundleService(configurationService.ConfigDirectory);
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _isAdministratorSession = IsAdministrator();
        _recommendedBaseline = AppCatalog.CreateRecommendedConfiguration();
        _runElapsedTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _runElapsedTimer.Tick += (_, _) => RaisePropertyChanged(nameof(RunElapsedText));
        _snapshotFreshnessTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _snapshotFreshnessTimer.Tick += (_, _) => RaiseSnapshotFreshnessChanged();
        _snapshotFreshnessTimer.Start();

        RecommendedHighlights = new ObservableCollection<string>(AppCatalog.RecommendedHighlights);
        ThemeNames = new ObservableCollection<string>(AppCatalog.ThemeSchemes.Keys);
        SchemeOptions = new ObservableCollection<string>(AppCatalog.ThemeSchemes[_selectedTheme]);
        LyricsThemes = new ObservableCollection<string>(AppCatalog.LyricsThemes);
        SpotifyVersionOptions = new ObservableCollection<AppCatalog.SpotifyVersionEntry>(AppCatalog.SpotifyVersionManifest);
        DownloadMethodOptions = new ObservableCollection<AppCatalog.DownloadMethodEntry>(AppCatalog.DownloadMethods);
        SelectionInsights = new ObservableCollection<SelectionInsightViewModel>();
        SelectedExtensionLabels = new ObservableCollection<string>();
        SupportBundleItems = new ObservableCollection<SupportBundleCategoryViewModel>();
        SupportBundleRedactionRules = new ObservableCollection<string>();

        InstallOptions = CreateOptions("Install");
        CoreOptions = CreateOptions("Core");
        InterfaceOptions = CreateOptions("Interface");
        AdvancedOptions = CreateOptions("Advanced");
        ExperienceOptions = CreateOptions("Experience");
        Extensions = new ObservableCollection<ExtensionToggleViewModel>(
            AppCatalog.ExtensionDefinitions.Select(def => new ExtensionToggleViewModel(
                def.Key,
                def.Title,
                def.Description,
                _recommendedBaseline.Spicetify_Extensions.Contains(def.Key, StringComparer.OrdinalIgnoreCase))));

        _maintenanceCards = AppCatalog.MaintenanceActions
            .Select(def => new MaintenanceActionCardViewModel(def, RunMaintenanceAsync, () => !IsRunning, HandleAsyncCommandException))
            .ToList();

        SafeMaintenanceActions = new ObservableCollection<MaintenanceActionCardViewModel>(_maintenanceCards.Where(card => !card.IsDestructive));
        DestructiveMaintenanceActions = new ObservableCollection<MaintenanceActionCardViewModel>(_maintenanceCards.Where(card => card.IsDestructive));

        LogEntries = new ObservableCollection<LogEntryViewModel>();
        ApplyRecommendedCommand = new AsyncRelayCommand(ApplyRecommendedAsync, () => !IsRunning, HandleAsyncCommandException);
        ApplyCustomCommand = new AsyncRelayCommand(ApplyCustomAsync, () => !IsRunning, HandleAsyncCommandException);
        CancelRunCommand = new RelayCommand(PresentCancelRunPrompt, () => IsRunning && !IsCancelRequested);
        DismissActivityCommand = new RelayCommand(DismissActivity, () => IsActivityVisible && !IsRunning);
        CopyLogCommand = new RelayCommand(CopyLog, () => LogEntries.Count > 0);
        OpenLibreSpotFolderCommand = new RelayCommand(OpenLibreSpotFolder);
        RefreshSnapshotCommand = new AsyncRelayCommand(RefreshSnapshotAsync, onException: HandleAsyncCommandException);
        RefreshSupportBundlePreviewCommand = new RelayCommand(RefreshSupportBundlePreview);
        ExportSupportBundleCommand = new AsyncRelayCommand(ExportSupportBundleAsync, () => !IsRunning, HandleAsyncCommandException);
        EnableAutoReapplyCommand = new RelayCommand(() => PresentAutoReapplyPrompt(enable: true), () => !IsRunning && !Snapshot.AutoReapplyTaskRegistered);
        DisableAutoReapplyCommand = new RelayCommand(() => PresentAutoReapplyPrompt(enable: false), () => !IsRunning && Snapshot.AutoReapplyTaskRegistered);
        ClearSettingsSearchCommand = new RelayCommand(() => SettingsSearchText = string.Empty, () => HasSettingsSearchText);
        RelaunchAsAdministratorCommand = new RelayCommand(PresentAdministratorPrompt, () => NeedsAdministratorRelaunch && !IsRunning);
        ConfirmPromptCommand = new AsyncRelayCommand(ConfirmPromptAsync, () => IsPromptVisible, HandleAsyncCommandException);
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

    public ObservableCollection<string> RecommendedHighlights { get; }
    public ObservableCollection<string> ThemeNames { get; }
    public ObservableCollection<string> SchemeOptions { get; }
    public ObservableCollection<string> LyricsThemes { get; }
    public ObservableCollection<AppCatalog.SpotifyVersionEntry> SpotifyVersionOptions { get; }
    public ObservableCollection<AppCatalog.DownloadMethodEntry> DownloadMethodOptions { get; }
    public ObservableCollection<SelectionInsightViewModel> SelectionInsights { get; }
    public ObservableCollection<string> SelectedExtensionLabels { get; }

    public ObservableCollection<OptionToggleViewModel> InstallOptions { get; }
    public ObservableCollection<OptionToggleViewModel> CoreOptions { get; }
    public ObservableCollection<OptionToggleViewModel> InterfaceOptions { get; }
    public ObservableCollection<OptionToggleViewModel> AdvancedOptions { get; }
    public ObservableCollection<OptionToggleViewModel> ExperienceOptions { get; }
    public ObservableCollection<ExtensionToggleViewModel> Extensions { get; }
    public ObservableCollection<MaintenanceActionCardViewModel> SafeMaintenanceActions { get; }
    public ObservableCollection<MaintenanceActionCardViewModel> DestructiveMaintenanceActions { get; }
    public ObservableCollection<SupportBundleCategoryViewModel> SupportBundleItems { get; }
    public ObservableCollection<string> SupportBundleRedactionRules { get; }
    public ObservableCollection<LogEntryViewModel> LogEntries { get; }

    public AsyncRelayCommand ApplyRecommendedCommand { get; }
    public AsyncRelayCommand ApplyCustomCommand { get; }
    public RelayCommand CancelRunCommand { get; }
    public RelayCommand DismissActivityCommand { get; }
    public RelayCommand CopyLogCommand { get; }
    public RelayCommand OpenLibreSpotFolderCommand { get; }
    public AsyncRelayCommand RefreshSnapshotCommand { get; }
    public RelayCommand RefreshSupportBundlePreviewCommand { get; }
    public AsyncRelayCommand ExportSupportBundleCommand { get; }
    public RelayCommand EnableAutoReapplyCommand { get; }
    public RelayCommand DisableAutoReapplyCommand { get; }
    public RelayCommand ClearSettingsSearchCommand { get; }
    public RelayCommand RelaunchAsAdministratorCommand { get; }
    public AsyncRelayCommand ConfirmPromptCommand { get; }
    public RelayCommand CancelPromptCommand { get; }
    public RelayCommand EscapeCommand { get; }

    public EnvironmentSnapshot Snapshot
    {
        get => _snapshot;
        private set
        {
            if (SetProperty(ref _snapshot, value))
            {
                RaiseAutoReapplyStateChanged();
                RefreshSupportBundlePreview();
            }
        }
    }

    public bool IsAdministratorSession => _isAdministratorSession;
    public bool NeedsAdministratorRelaunch => !_isAdministratorSession;

    public string SessionAccessTitle =>
        IsAdministratorSession ? "Ready to run" : "Admin step needed";

    public string SessionAccessDetail =>
        IsAdministratorSession
            ? "LibreSpot can patch and recover without another Windows prompt."
            : "You can review settings now. LibreSpot asks for elevation only when you start.";

    public string SpotifyStatusLine =>
        Snapshot.SpotifyInstalled
            ? "Spotify detected"
            : "Spotify not installed";

    public string CustomizationStatusLine =>
        Snapshot.SpicetifyInstalled
            ? "Spicetify detected"
            : "Spicetify not installed";

    public string MarketplaceStatusLine =>
        !Snapshot.SpicetifyInstalled
            ? "Marketplace unavailable until Spicetify is installed"
            : Snapshot.MarketplaceReady
                ? "Marketplace ready"
                : Snapshot.MarketplaceFilesPresent
                    ? "Marketplace hidden - repair available"
                    : Snapshot.MarketplaceRegistered
                        ? "Marketplace files missing - repair available"
                        : "Marketplace not enabled";

    public StackHealthReport HealthReport => Snapshot.HealthReport;
    public IReadOnlyList<StackHealthComponent> CriticalHealthIssues => HealthReport.CriticalIssues;
    public IReadOnlyList<StackHealthComponent> WarningHealthIssues => HealthReport.WarningIssues;
    public IReadOnlyList<StackHealthComponent> InfoHealthIssues => HealthReport.InfoIssues;
    public bool HasCriticalHealthIssues => HealthReport.HasCriticalIssues;
    public bool HasWarningHealthIssues => HealthReport.HasWarningIssues;
    public bool HasInfoHealthIssues => HealthReport.HasInfoIssues;
    public bool HasAnyHealthIssues => HealthReport.HasIssues;
    public string HealthIssueSummary => HealthReport.IssueSummary;

    public bool HasConfigurationRecoveryNotice =>
        _configurationLoadState == ConfigurationLoadState.RecoveredFromCorrupt;

    private bool IsForwardIncompatibleConfiguration =>
        _configurationRecoveryReason?.Contains("newer than this LibreSpot build supports", StringComparison.OrdinalIgnoreCase) == true;

    public string ConfigurationRecoveryTitle =>
        IsForwardIncompatibleConfiguration
            ? "Profile needs a newer LibreSpot"
            : "Unreadable profile was recovered";

    private string ConfigurationRecoveryReasonClause =>
        string.IsNullOrWhiteSpace(_configurationRecoveryReason)
            ? string.Empty
            : $" Reason: {_configurationRecoveryReason.Trim()}";

    public string ConfigurationRecoveryDetail =>
        !HasConfigurationRecoveryNotice
            ? string.Empty
            : IsForwardIncompatibleConfiguration
                ? string.IsNullOrWhiteSpace(_recoveredConfigurationPath)
                    ? $"LibreSpot loaded safe defaults because config.json was saved by a newer build.{ConfigurationRecoveryReasonClause} Update LibreSpot before reusing that profile."
                    : $"LibreSpot kept the config.json from a newer LibreSpot build aside and loaded safe defaults.{ConfigurationRecoveryReasonClause} Update LibreSpot before reusing that profile. Backup kept as {Path.GetFileName(_recoveredConfigurationPath)}."
            : string.IsNullOrWhiteSpace(_recoveredConfigurationPath)
                ? $"LibreSpot reopened with safe defaults because config.json could not be read safely.{ConfigurationRecoveryReasonClause} Apply Recommended or Custom to save a fresh profile."
                : $"LibreSpot moved the unreadable config.json aside and reopened with safe defaults.{ConfigurationRecoveryReasonClause} Apply Recommended or Custom to save a fresh profile. Backup kept as {Path.GetFileName(_recoveredConfigurationPath)}.";

    public string ProfileStatusLine =>
        HasConfigurationRecoveryNotice
            ? "Recovered defaults loaded"
            : Snapshot.SavedConfigExists
            ? "Saved LibreSpot profile found"
            : "No saved profile yet";

    public string AutoReapplyStatusTitle =>
        Snapshot.AutoReapplyTaskRegistered
            ? "Auto-reapply watcher active"
            : "Auto-reapply watcher off";

    public string AutoReapplyStatusDetail =>
        Snapshot.AutoReapplyTaskRegistered
            ? "LibreSpot checks after Windows sign-in and every 30 minutes, then reapplies only after Spotify updates while Spotify is closed."
            : "Enable the watcher to restore your saved SpotX and Spicetify setup after Spotify updates itself.";

    public string AutoReapplyTaskLine =>
        Snapshot.AutoReapplyTaskRegistered
            ? @"Task: LibreSpot\ReapplyWatcher registered"
            : @"Task: LibreSpot\ReapplyWatcher not registered";

    public string AutoReapplyLogLine =>
        $"Log: {Path.Combine(_configurationService.ConfigDirectory, "watcher.log")}";

    public string WorkspaceRecommendationTitle =>
        HasConfigurationRecoveryNotice
            ? "Save a fresh baseline"
            : Snapshot.SpotifyInstalled && Snapshot.SpicetifyInstalled
            ? "Stabilize or fine-tune"
            : Snapshot.SpotifyInstalled
                ? "Finish setup"
                : "Start here";

    public string WorkspaceRecommendationDetail =>
        HasConfigurationRecoveryNotice
            ? "LibreSpot recovered from an unreadable profile. Recommended writes a clean baseline again, or you can inspect the backup first."
            : Snapshot.SpotifyInstalled && Snapshot.SpicetifyInstalled
            ? "Recommended is still the fastest way back to a clean baseline. Move to Custom once you know what you want to keep."
            : Snapshot.SpotifyInstalled
                ? "Spotify is already installed. Recommended restores the supported customization layer with the least friction."
                : "No Spotify stack yet. Recommended is the safest starting point.";

    public string WorkspaceRecommendationBrief =>
        HasConfigurationRecoveryNotice
            ? "Best way to save a clean profile again."
            : Snapshot.SpotifyInstalled && Snapshot.SpicetifyInstalled
                ? "Safest path before fine-tuning."
                : Snapshot.SpotifyInstalled
                    ? "Fastest way to restore the supported layer."
                    : "Fastest way to lay down the known-good baseline.";

    public string CustomSelectionSummary
    {
        get
        {
            var changeCount = CountProfileDifferencesFromRecommended();
            return changeCount switch
            {
                0 => "Still aligned with the stable Recommended baseline.",
                1 => "1 deliberate change from the default stable profile.",
                _ => $"{changeCount} deliberate changes from the default stable profile."
            };
        }
    }

    public string InstallPostureLabel =>
        IsOptionSelected(nameof(InstallConfiguration.CleanInstall))
            ? "Clean start"
            : "Overlay";

    public string EnabledToggleCountLabel =>
        $"{EnumerateAllOptions().Count(option => option.IsSelected)} enabled";

    public string CustomChangeCountLabel
    {
        get
        {
            var changeCount = CountProfileDifferencesFromRecommended();
            return changeCount switch
            {
                0 => "Matches Recommended",
                1 => "1 change",
                _ => $"{changeCount} changes"
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
                0 => "None",
                1 => "1 selected",
                _ => $"{selectedCount} selected"
            };
        }
    }

    public string AccessPostureLabel =>
        NeedsAdministratorRelaunch
            ? "Elevates first"
            : "Current session";

    public bool HasSelectedExtensions => SelectedExtensionLabels.Count > 0;

    public string ThemeSummary =>
        SelectedTheme == "(None - Marketplace Only)"
            ? "Marketplace-only (no theme pack)"
            : $"{SelectedTheme} · {Prettify.Label(SelectedScheme)}";

    public bool IsThemeSchemeAvailable => !string.Equals(SelectedTheme, "(None - Marketplace Only)", StringComparison.Ordinal);

    public string ThemeSchemeHint =>
        IsThemeSchemeAvailable
            ? "Color scheme inside the selected theme pack."
            : "Marketplace-only skips the theme pack, so no extra scheme is applied.";

    public string LyricsSummary => $"Lyrics: {Prettify.Label(SelectedLyricsTheme)}";

    public bool IsLyricsThemeAvailable => IsOptionSelected(nameof(InstallConfiguration.SpotX_LyricsEnabled));

    public string LyricsThemeHint =>
        IsLyricsThemeAvailable
            ? "SpotX restores this lyrics skin after patching."
            : "Turn on the lyrics patch to make this selection matter.";

    public string CacheSummary =>
        int.TryParse(CacheLimitText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? $"Cache ceiling: {parsed} MB"
            : "Cache: default";

    public string SpotifyVersionSummary => CurrentSpotifyVersionEntry.Label;

    public string SpotifyVersionNotes => CurrentSpotifyVersionEntry.Notes;

    public string? ArchitectureMismatchWarning =>
        AppCatalog.CheckArchitectureCompatibility(CurrentSpotifyVersionEntry, Snapshot.HostArchitecture);

    public bool HasArchitectureMismatch => !string.IsNullOrEmpty(ArchitectureMismatchWarning);

    public string DownloadMethodSummary => CurrentDownloadMethodEntry.Label;

    public string DownloadMethodDetail => CurrentDownloadMethodEntry.Detail;

    public string SettingsSearchText
    {
        get => _settingsSearchText;
        set
        {
            if (SetProperty(ref _settingsSearchText, value))
            {
                RefreshSettingsSearch();
            }
        }
    }

    public bool HasSettingsSearchText => !string.IsNullOrWhiteSpace(SettingsSearchText);

    public bool HasVisibleInstallOptions => HasVisibleOptions(InstallOptions);

    public bool HasVisibleAppearanceSettings => CountAppearanceMatches() > 0;

    public bool HasVisibleCoreOptions => HasVisibleOptions(CoreOptions);

    public bool HasVisibleInterfaceOptions => HasVisibleOptions(InterfaceOptions);

    public bool HasVisibleBehaviorSection => HasVisibleCoreOptions || HasVisibleInterfaceOptions;

    public bool HasVisibleAdvancedOptions => HasVisibleOptions(AdvancedOptions);

    public bool HasVisibleExperienceOptions => HasVisibleOptions(ExperienceOptions);

    public bool HasVisibleAdvancedSection => HasVisibleAdvancedOptions || HasVisibleExperienceOptions;

    public bool HasVisibleExtensions => Extensions.Any(extension => MatchesSettingsSearch(extension.Title, extension.Description));

    public int CustomSearchMatchCount =>
        CountMatchingOptions(InstallOptions) +
        CountAppearanceMatches() +
        CountMatchingOptions(CoreOptions) +
        CountMatchingOptions(InterfaceOptions) +
        CountMatchingOptions(AdvancedOptions) +
        CountMatchingOptions(ExperienceOptions) +
        Extensions.Count(extension => MatchesSettingsSearch(extension.Title, extension.Description));

    public bool HasAnyCustomSearchMatches => !HasSettingsSearchText || CustomSearchMatchCount > 0;

    public bool ShowCustomSearchEmptyState => HasSettingsSearchText && !HasAnyCustomSearchMatches;

    public string CustomSearchSummary =>
        HasSettingsSearchText
            ? CustomSearchMatchCount switch
            {
                0 => $"No matches for \"{SettingsSearchText.Trim()}\".",
                1 => $"1 match for \"{SettingsSearchText.Trim()}\".",
                _ => $"{CustomSearchMatchCount} matches for \"{SettingsSearchText.Trim()}\"."
            }
            : "Search titles and descriptions across Custom.";

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
                0 => "None selected",
                1 => "1 extension selected",
                _ => $"{selectedCount} extensions selected"
            };
        }
    }

    public string MaintenanceGuidanceTitle =>
        Snapshot.SpotifyInstalled && Snapshot.SpicetifyInstalled
            ? "Ready for upkeep or reset"
            : Snapshot.SpotifyInstalled
                ? "Customization layer is incomplete"
                : "Maintenance is ready when you need it";

    public string MaintenanceGuidanceDetail =>
        Snapshot.SpotifyInstalled && Snapshot.SpicetifyInstalled
            ? "Start with the safer actions. Use reset only when you want to clear the stack and rebuild."
            : Snapshot.SpotifyInstalled
                ? "Reapply becomes useful once the customization layer is back in place. Until then, Recommended is usually the better path."
                : "Most repair actions matter after Spotify is installed. You can still inspect versions, export support details, or prepare a clean reset.";

    private int MaintenanceReadyComponentCount =>
        new[] { "spotify", "spotx", "spicetify-cli", "marketplace", "active-theme" }
            .Count(id => HealthComponent(id)?.Severity == HealthSeverity.Ready);

    public string MaintenanceReadinessValue => $"{MaintenanceReadyComponentCount} of 5 ready";

    public string MaintenanceReadinessDetail =>
        MaintenanceReadyComponentCount switch
        {
            5 => "Spotify, SpotX, Spicetify, Marketplace, and theme state are all ready.",
            0 => "No customization stack components are ready yet.",
            _ => "Spotify, SpotX, Spicetify, Marketplace, and theme checks are partially ready."
        };

    public string MaintenanceBackupValue => HealthComponent("backups")?.Status ?? "Unknown";

    public string MaintenanceBackupDetail
    {
        get
        {
            var backups = HealthComponent("backups");
            if (backups is null)
            {
                return "Backup state has not been checked yet.";
            }

            return backups.HasLastChanged
                ? $"{backups.Evidence} Latest: {backups.LastChangedDisplay}."
                : backups.Evidence;
        }
    }

    public string MaintenanceMarketplaceValue => HealthComponent("marketplace")?.Status ?? "Unknown";
    public string MaintenanceMarketplaceDetail => HealthComponent("marketplace")?.Evidence ?? "Marketplace state has not been checked yet.";
    public string MaintenanceThemeValue => HealthComponent("active-theme")?.Status ?? "Unknown";
    public string MaintenanceThemeDetail => HealthComponent("active-theme")?.Evidence ?? "Theme state has not been checked yet.";

    public string SupportBundlePreviewTitle =>
        _supportBundlePreview.SelectedFileCount switch
        {
            0 => "Health report only",
            1 => "1 diagnostic file selected",
            _ => $"{_supportBundlePreview.SelectedFileCount} diagnostic files selected"
        };

    public string SupportBundlePreviewDetail =>
        $"Estimated local zip size before compression: {FormatBytes(_supportBundlePreview.EstimatedBytes)}.";

    public string SupportBundleRedactionSummary =>
        "User paths, machine/user names, GitHub headers, proxy credentials, tokens, passwords, and command-line secret arguments are redacted before the zip is written.";

    public string SupportBundleLastExportText
    {
        get => _supportBundleLastExportText;
        private set => SetProperty(ref _supportBundleLastExportText, value);
    }

    public string RecommendedRunDuration =>
        Snapshot.SpotifyInstalled
            ? "Usually 2-3 minutes, depending on whether Spotify restarts."
            : "Usually 3-4 minutes because LibreSpot may need to lay down the full stack first.";

    public string RecommendedFollowUpText =>
        HasConfigurationRecoveryNotice
            ? "This run writes a fresh config.json and keeps the recovered backup in the LibreSpot folder for reference."
            : Snapshot.SavedConfigExists
            ? "The saved LibreSpot profile stays aligned with this run, so reapply and maintenance stay predictable later."
            : "LibreSpot will create the first saved profile during this run so maintenance has a dependable baseline.";

    public string CustomProfileTitle
    {
        get
        {
            var advancedCount = AdvancedOptions.Count(option => option.IsSelected);
            var selectedExtensions = Extensions.Count(item => item.IsSelected);

            return advancedCount switch
            {
                0 when selectedExtensions <= 3 => "Near default",
                <= 2 => "Balanced custom",
                _ => "Heavy custom"
            };
        }
    }

    public string CustomProfileDetail
    {
        get
        {
            var advancedCount = AdvancedOptions.Count(option => option.IsSelected);
            var selectedExtensions = Extensions.Count(item => item.IsSelected);

            if (advancedCount == 0 && selectedExtensions <= 3)
            {
                return "This stays close to LibreSpot's supported baseline with room for a few deliberate preferences.";
            }

            if (advancedCount <= 2)
            {
                return "You are personalizing the stack without moving far beyond what is usually easy to reapply after updates.";
            }

            return "Several advanced toggles are active, so expect more upkeep after Spotify changes.";
        }
    }

    public string CustomRunReadinessTitle
    {
        get
        {
            if (NeedsAdministratorRelaunch)
            {
                return "Admin step first";
            }

            if (HasConfigurationRecoveryNotice)
            {
                return "Fresh profile ready";
            }

            if (HasConflictingSidebarOptions())
            {
                return "Review one conflict";
            }

            if (!IsOptionSelected(nameof(InstallConfiguration.CleanInstall)) && !Snapshot.SpotifyInstalled)
            {
                return "Best on an existing install";
            }

            return "Ready";
        }
    }

    public string CustomRunReadinessDetail
    {
        get
        {
            if (NeedsAdministratorRelaunch)
            {
                return "LibreSpot asks Windows for administrator access before it patches Spotify.";
            }

            if (HasConfigurationRecoveryNotice)
            {
                return "Applying now writes a fresh config.json and keeps the recovered backup untouched for reference.";
            }

            if (HasConflictingSidebarOptions())
            {
                return "Hide right sidebar and clear right sidebar styling are both selected. Hiding the sidebar wins, so the styling option adds noise.";
            }

            if (!IsOptionSelected(nameof(InstallConfiguration.CleanInstall)) && !Snapshot.SpotifyInstalled)
            {
                return "Skipping a clean start usually helps only when Spotify is already installed. Recommended is safer on a blank machine.";
            }

            return "LibreSpot saves this profile first, then applies it so maintenance and reapply stay in sync.";
        }
    }

    public string CustomApplyCaption =>
        NeedsAdministratorRelaunch
            ? "LibreSpot relaunches with administrator access before it touches Spotify."
            : HasConfigurationRecoveryNotice
                ? "LibreSpot replaces the unreadable config.json with this profile and keeps the recovered copy in the LibreSpot folder."
            : HasConflictingSidebarOptions()
                ? "You can still apply this profile, but the overlapping right-sidebar toggles are worth simplifying first."
                : "LibreSpot saves this profile to config.json, then applies it through the original backend.";

    public bool IsOverviewWorkspaceSelected => SelectedWorkspaceIndex == 0;

    public string WorkspaceHeroEyebrow => SelectedWorkspaceIndex switch
    {
        1 => "Custom profile",
        2 => "Recovery lane",
        _ => "Guided setup"
    };

    public string WorkspaceHeroTitle => SelectedWorkspaceIndex switch
    {
        1 => "Custom settings",
        2 => "Maintenance",
        _ => "Recommended setup"
    };

    public string WorkspaceHeroBody => SelectedWorkspaceIndex switch
    {
        1 => "Adjust supported options without losing track of the baseline.",
        2 => "Use repair, rollback, and reset tools when the stack needs attention.",
        _ => "Start with the quickest stable path, then move to Custom only when you know what to change."
    };

    public int SelectedWorkspaceIndex
    {
        get => _selectedWorkspaceIndex;
        set
        {
            if (SetProperty(ref _selectedWorkspaceIndex, value))
            {
                RaisePropertyChanged(nameof(IsOverviewWorkspaceSelected));
                RaisePropertyChanged(nameof(WorkspaceHeroEyebrow));
                RaisePropertyChanged(nameof(WorkspaceHeroTitle));
                RaisePropertyChanged(nameof(WorkspaceHeroBody));
            }
        }
    }

    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                RebuildSchemes();
                RaiseSelectionInsightsChanged();
            }
        }
    }

    public string SelectedScheme
    {
        get => _selectedScheme;
        set
        {
            if (SetProperty(ref _selectedScheme, value))
            {
                RaiseSelectionInsightsChanged();
            }
        }
    }

    public string SelectedLyricsTheme
    {
        get => _selectedLyricsTheme;
        set
        {
            if (SetProperty(ref _selectedLyricsTheme, value))
            {
                RaiseSelectionInsightsChanged();
            }
        }
    }

    public string SelectedSpotifyVersionId
    {
        get => _selectedSpotifyVersionId;
        set
        {
            if (SetProperty(ref _selectedSpotifyVersionId, value))
            {
                RaiseSelectionInsightsChanged();
            }
        }
    }

    public string SelectedDownloadMethod
    {
        get => _selectedDownloadMethod;
        set
        {
            if (SetProperty(ref _selectedDownloadMethod, value))
            {
                RaiseSelectionInsightsChanged();
            }
        }
    }

    public string CacheLimitText
    {
        get => _cacheLimitText;
        set
        {
            if (SetProperty(ref _cacheLimitText, value))
            {
                RaiseSelectionInsightsChanged();
            }
        }
    }

    public bool IsActivityVisible
    {
        get => _isActivityVisible;
        private set
        {
            if (SetProperty(ref _isActivityVisible, value))
            {
                DismissActivityCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                ApplyRecommendedCommand.RaiseCanExecuteChanged();
                ApplyCustomCommand.RaiseCanExecuteChanged();
                CancelRunCommand.RaiseCanExecuteChanged();
                DismissActivityCommand.RaiseCanExecuteChanged();
                EnableAutoReapplyCommand.RaiseCanExecuteChanged();
                DisableAutoReapplyCommand.RaiseCanExecuteChanged();
                ExportSupportBundleCommand.RaiseCanExecuteChanged();
                RelaunchAsAdministratorCommand.RaiseCanExecuteChanged();
                ConfirmPromptCommand.RaiseCanExecuteChanged();
                CancelPromptCommand.RaiseCanExecuteChanged();
                RaiseMaintenanceActionCanExecuteChanged();
                RaisePropertyChanged(nameof(IsBusyIndeterminate));
                RaisePropertyChanged(nameof(ProgressLabel));
                RaisePropertyChanged(nameof(IsActivityError));
                RaisePropertyChanged(nameof(IsActivityCanceled));
                RaisePropertyChanged(nameof(ActivityBadgeText));
                RaisePropertyChanged(nameof(ActivityDetailLabel));
                RaisePropertyChanged(nameof(ActivityAssistiveText));
                RaisePropertyChanged(nameof(ActivitySummaryTitle));
                RaisePropertyChanged(nameof(TaskbarProgressState));
                RaisePropertyChanged(nameof(TaskbarProgressFraction));
            }
        }
    }

    public bool IsCancelRequested
    {
        get => _isCancelRequested;
        private set
        {
            if (SetProperty(ref _isCancelRequested, value))
            {
                CancelRunCommand.RaiseCanExecuteChanged();
                RaisePropertyChanged(nameof(ProgressLabel));
                RaisePropertyChanged(nameof(IsActivityCanceled));
                RaisePropertyChanged(nameof(ActivityBadgeText));
                RaisePropertyChanged(nameof(ActivityDetailLabel));
                RaisePropertyChanged(nameof(ActivityAssistiveText));
                RaisePropertyChanged(nameof(ActivitySummaryTitle));
                RaisePropertyChanged(nameof(TaskbarProgressState));
            }
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set
        {
            if (SetProperty(ref _progressValue, value))
            {
                RaisePropertyChanged(nameof(ProgressLabel));
                RaisePropertyChanged(nameof(IsBusyIndeterminate));
                RaisePropertyChanged(nameof(IsActivityCanceled));
                RaisePropertyChanged(nameof(ActivityBadgeText));
                RaisePropertyChanged(nameof(ActivityDetailLabel));
                RaisePropertyChanged(nameof(ActivityAssistiveText));
                RaisePropertyChanged(nameof(ActivitySummaryTitle));
                RaisePropertyChanged(nameof(TaskbarProgressState));
                RaisePropertyChanged(nameof(TaskbarProgressFraction));
            }
        }
    }

    public bool IsBusyIndeterminate => _isRunning && _progressValue <= 0.0;

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
            if (!_isRunning)
            {
                return System.Windows.Shell.TaskbarItemProgressState.None;
            }
            return IsBusyIndeterminate
                ? System.Windows.Shell.TaskbarItemProgressState.Indeterminate
                : System.Windows.Shell.TaskbarItemProgressState.Normal;
        }
    }

    // TaskbarItemInfo.ProgressValue expects 0.0..1.0, but our ProgressValue is 0..100.
    public double TaskbarProgressFraction => Math.Clamp(_progressValue / 100.0, 0.0, 1.0);

    // "— %" reads like a broken UI. When we don't yet have a real percentage
    // from the backend, say what is actually happening: we're working.
    public string ProgressLabel =>
        IsCancelRequested
            ? "Stopping…"
            : IsBusyIndeterminate
            ? "Working…"
            : _isRunning
                ? $"{Math.Round(ProgressValue)}%"
                : _progressValue >= 100 ? "Done" : "Ready";

    // Activity badge surfaces the run's outcome after completion so the overlay
    // isn't frozen on "Live run" once work is done. We derive from ActivityStatus
    // because HandleBackendMessage already reconciles status strings per outcome.
    public bool IsActivityError =>
        !_isRunning &&
        !string.IsNullOrEmpty(_activityStatus) &&
        (_activityStatus.Contains("attention", StringComparison.OrdinalIgnoreCase) ||
         _activityStatus.Contains("failed", StringComparison.OrdinalIgnoreCase));

    public bool IsActivityCanceled =>
        !_isRunning &&
        !string.IsNullOrEmpty(_activityStatus) &&
        _activityStatus.Contains("canceled", StringComparison.OrdinalIgnoreCase);

    public string ActivityBadgeText =>
        IsCancelRequested ? "Stopping"
        : _isRunning ? "In progress"
        : IsActivityCanceled ? "Canceled"
        : IsActivityError ? "Needs review"
        : _progressValue >= 100 ? "Complete"
        : "Ready";

    public string ActivityDetailLabel =>
        IsRunning || IsCancelRequested
            ? "Current step"
            : "Run status";

    public string ActivityTitle
    {
        get => _activityTitle;
        private set => SetProperty(ref _activityTitle, value);
    }

    public string ActivityStatus
    {
        get => _activityStatus;
        private set
        {
            if (SetProperty(ref _activityStatus, value))
            {
                RaisePropertyChanged(nameof(IsActivityError));
                RaisePropertyChanged(nameof(IsActivityCanceled));
                RaisePropertyChanged(nameof(ActivityBadgeText));
                RaisePropertyChanged(nameof(ActivityDetailLabel));
                RaisePropertyChanged(nameof(ActivityAssistiveText));
                RaisePropertyChanged(nameof(ActivitySummaryTitle));
                RaisePropertyChanged(nameof(TaskbarProgressState));
            }
        }
    }

    public string ActivityStep
    {
        get => _activityStep;
        private set => SetProperty(ref _activityStep, value);
    }

    public string ActivityAssistiveText =>
        IsCancelRequested
            ? "LibreSpot is stopping the backend and preserving the log gathered so far."
            : IsRunning
                ? "LibreSpot keeps the live log and diagnostics on disk while this runs. You can cancel here if you need to stop early."
                : IsActivityCanceled
                    ? "LibreSpot stopped early. Review the log, then rerun Recommended or Reapply if Spotify looks inconsistent."
                : IsActivityError
                    ? "Open the LibreSpot folder or copy the log before retrying so the next run starts with better context."
                    : _progressValue >= 100
                        ? "Your saved profile and maintenance tools are ready for the next pass."
                        : "You can dismiss this panel or copy the log for reference.";

    public string ActivitySummaryTitle =>
        IsCancelRequested
            ? "Stopping safely"
            : IsRunning
                ? "While this runs"
                : IsActivityCanceled || IsActivityError
                    ? "Recommended next step"
                    : _progressValue >= 100
                        ? "Next step"
                        : "Session details";

    public string ActivityLogPathText => $"Log file: {_configurationService.LogPath}";

    public string RunElapsedText =>
        _runStopwatch.Elapsed.TotalHours >= 1
            ? _runStopwatch.Elapsed.ToString(@"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture)
            : _runStopwatch.Elapsed.ToString(@"mm\:ss", System.Globalization.CultureInfo.InvariantCulture);

    public string LogLineCountText =>
        LogEntries.Count switch
        {
            0 => "No log output yet",
            1 => "1 log line",
            _ => $"{LogEntries.Count} log lines"
        };

    public bool IsLogEmpty => LogEntries.Count == 0;

    public string LastRefreshedText =>
        _snapshotRefreshedAt is null
            ? string.Empty
            : $"Last refreshed {_snapshotRefreshedAt.Value.ToString("h:mm:ss tt", System.Globalization.CultureInfo.InvariantCulture)}";

    public bool IsSnapshotStale =>
        _snapshotRefreshedAt is not null &&
        DateTime.Now - _snapshotRefreshedAt.Value >= TimeSpan.FromMinutes(5);

    public string SnapshotFreshnessTitle
    {
        get
        {
            if (_snapshotRefreshedAt is null)
            {
                return "Status not checked yet";
            }

            var age = DateTime.Now - _snapshotRefreshedAt.Value;
            if (age < TimeSpan.FromMinutes(1))
            {
                return "Environment checked just now";
            }

            if (age < TimeSpan.FromMinutes(3))
            {
                return "Environment checked recently";
            }

            return IsSnapshotStale ? "Refresh recommended" : "Environment may have changed";
        }
    }

    public string SnapshotFreshnessDetail
    {
        get
        {
            if (_snapshotRefreshedAt is null)
            {
                return "Use Refresh environment before you decide whether Spotify, Spicetify, or the saved profile need repair.";
            }

            var refreshedAt = _snapshotRefreshedAt.Value.ToString("h:mm:ss tt", System.Globalization.CultureInfo.InvariantCulture);
            return IsSnapshotStale
                ? $"Last checked at {refreshedAt}. Recheck before you repair or reset if anything changed outside LibreSpot."
                : $"Last checked at {refreshedAt}. Refresh after you change Spotify outside LibreSpot.";
        }
    }

    public bool IsPromptVisible
    {
        get => _isPromptVisible;
        private set
        {
            if (SetProperty(ref _isPromptVisible, value))
            {
                ConfirmPromptCommand.RaiseCanExecuteChanged();
                CancelPromptCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string PromptTitle
    {
        get => _promptTitle;
        private set => SetProperty(ref _promptTitle, value);
    }

    public string PromptBody
    {
        get => _promptBody;
        private set => SetProperty(ref _promptBody, value);
    }

    public string PromptConfirmText
    {
        get => _promptConfirmText;
        private set => SetProperty(ref _promptConfirmText, value);
    }

    public string PromptCancelText
    {
        get => _promptCancelText;
        private set => SetProperty(ref _promptCancelText, value);
    }

    public string PromptSummaryTitle
    {
        get => _promptSummaryTitle;
        private set => SetProperty(ref _promptSummaryTitle, value);
    }

    public string PromptSummaryBody
    {
        get => _promptSummaryBody;
        private set => SetProperty(ref _promptSummaryBody, value);
    }

    public bool IsPromptDestructive
    {
        get => _isPromptDestructive;
        private set => SetProperty(ref _isPromptDestructive, value);
    }

    public async Task InitializeAsync()
    {
        var loadResult = await _configurationService.LoadResultAsync();
        _configurationLoadState = loadResult.State;
        _recoveredConfigurationPath = loadResult.RecoveredFilePath;
        _configurationRecoveryReason = loadResult.RecoveryReason;
        ApplyConfigurationToEditor(loadResult.Configuration);
        await RefreshSnapshotAsync();
    }

    private ObservableCollection<OptionToggleViewModel> CreateOptions(string section) =>
        new(AppCatalog.OptionDefinitions
            .Where(def => def.Section == section)
            .Select(def => new OptionToggleViewModel(
                def.Key,
                def.Title,
                def.Description,
                typeof(InstallConfiguration).GetProperty(def.Key, BindingFlags.Public | BindingFlags.Instance)?.GetValue(_recommendedBaseline) is bool value && value)));

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
        ClearSettingsSearchCommand.RaiseCanExecuteChanged();
        RaiseCustomSearchChanged();
    }

    private void RaiseCustomSearchChanged()
    {
        RaisePropertyChanged(nameof(HasSettingsSearchText));
        RaisePropertyChanged(nameof(HasVisibleInstallOptions));
        RaisePropertyChanged(nameof(HasVisibleAppearanceSettings));
        RaisePropertyChanged(nameof(HasVisibleCoreOptions));
        RaisePropertyChanged(nameof(HasVisibleInterfaceOptions));
        RaisePropertyChanged(nameof(HasVisibleBehaviorSection));
        RaisePropertyChanged(nameof(HasVisibleAdvancedOptions));
        RaisePropertyChanged(nameof(HasVisibleExperienceOptions));
        RaisePropertyChanged(nameof(HasVisibleAdvancedSection));
        RaisePropertyChanged(nameof(HasVisibleExtensions));
        RaisePropertyChanged(nameof(CustomSearchMatchCount));
        RaisePropertyChanged(nameof(HasAnyCustomSearchMatches));
        RaisePropertyChanged(nameof(ShowCustomSearchEmptyState));
        RaisePropertyChanged(nameof(CustomSearchSummary));
    }

    private int CountMatchingOptions(IEnumerable<OptionToggleViewModel> options) =>
        options.Count(option => MatchesSettingsSearch(option.Title, option.Description));

    private bool HasVisibleOptions(IEnumerable<OptionToggleViewModel> options) =>
        options.Any(option => MatchesSettingsSearch(option.Title, option.Description));

    private int CountAppearanceMatches()
    {
        var count = 0;
        count += MatchesSettingsSearch("Theme pack", "Choose the Spicetify theme pack LibreSpot restores.") ? 1 : 0;
        count += MatchesSettingsSearch("Color scheme", ThemeSchemeHint) ? 1 : 0;
        count += MatchesSettingsSearch("Lyrics theme", LyricsThemeHint) ? 1 : 0;
        count += MatchesSettingsSearch("Cache limit", "Leave 0 for the default cache behavior.") ? 1 : 0;
        count += MatchesSettingsSearch("Spotify build", SpotifyVersionNotes) ? 1 : 0;
        count += MatchesSettingsSearch("Download path", DownloadMethodDetail) ? 1 : 0;
        return count;
    }

    private bool MatchesSettingsSearch(string title, string description)
    {
        if (!HasSettingsSearchText)
        {
            return true;
        }

        var query = SettingsSearchText.Trim();
        return title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               description.Contains(query, StringComparison.OrdinalIgnoreCase);
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

    private IEnumerable<OptionToggleViewModel> EnumerateAllOptions()
    {
        foreach (var option in InstallOptions)
        {
            yield return option;
        }

        foreach (var option in CoreOptions)
        {
            yield return option;
        }

        foreach (var option in InterfaceOptions)
        {
            yield return option;
        }

        foreach (var option in AdvancedOptions)
        {
            yield return option;
        }

        foreach (var option in ExperienceOptions)
        {
            yield return option;
        }
    }

    private void RaiseSelectionInsightsChanged()
    {
        RebuildSelectionInsights();
        RaisePropertyChanged(nameof(CustomSelectionSummary));
        RaisePropertyChanged(nameof(InstallPostureLabel));
        RaisePropertyChanged(nameof(EnabledToggleCountLabel));
        RaisePropertyChanged(nameof(IsThemeSchemeAvailable));
        RaisePropertyChanged(nameof(ThemeSchemeHint));
        RaisePropertyChanged(nameof(ThemeSummary));
        RaisePropertyChanged(nameof(IsLyricsThemeAvailable));
        RaisePropertyChanged(nameof(LyricsThemeHint));
        RaisePropertyChanged(nameof(LyricsSummary));
        RaisePropertyChanged(nameof(CacheSummary));
        RaisePropertyChanged(nameof(SpotifyVersionSummary));
        RaisePropertyChanged(nameof(SpotifyVersionNotes));
        RaisePropertyChanged(nameof(ArchitectureMismatchWarning));
        RaisePropertyChanged(nameof(HasArchitectureMismatch));
        RaisePropertyChanged(nameof(DownloadMethodSummary));
        RaisePropertyChanged(nameof(DownloadMethodDetail));
        RaisePropertyChanged(nameof(ExtensionSummary));
        RaisePropertyChanged(nameof(SelectedExtensionCountLabel));
        RaisePropertyChanged(nameof(HasSelectedExtensions));
        RaisePropertyChanged(nameof(AccessPostureLabel));
        RaisePropertyChanged(nameof(CustomChangeCountLabel));
        RaisePropertyChanged(nameof(CustomProfileTitle));
        RaisePropertyChanged(nameof(CustomProfileDetail));
        RaisePropertyChanged(nameof(CustomRunReadinessTitle));
        RaisePropertyChanged(nameof(CustomRunReadinessDetail));
        RaisePropertyChanged(nameof(CustomApplyCaption));
    }

    private void RaiseSnapshotInsightsChanged()
    {
        RefreshMaintenanceActionRelevance();
        RaisePropertyChanged(nameof(SessionAccessTitle));
        RaisePropertyChanged(nameof(SessionAccessDetail));
        RaisePropertyChanged(nameof(SpotifyStatusLine));
        RaisePropertyChanged(nameof(CustomizationStatusLine));
        RaisePropertyChanged(nameof(MarketplaceStatusLine));
        RaisePropertyChanged(nameof(HealthReport));
        RaisePropertyChanged(nameof(CriticalHealthIssues));
        RaisePropertyChanged(nameof(WarningHealthIssues));
        RaisePropertyChanged(nameof(InfoHealthIssues));
        RaisePropertyChanged(nameof(HasCriticalHealthIssues));
        RaisePropertyChanged(nameof(HasWarningHealthIssues));
        RaisePropertyChanged(nameof(HasInfoHealthIssues));
        RaisePropertyChanged(nameof(HasAnyHealthIssues));
        RaisePropertyChanged(nameof(HealthIssueSummary));
        RaisePropertyChanged(nameof(MaintenanceReadinessValue));
        RaisePropertyChanged(nameof(MaintenanceReadinessDetail));
        RaisePropertyChanged(nameof(MaintenanceBackupValue));
        RaisePropertyChanged(nameof(MaintenanceBackupDetail));
        RaisePropertyChanged(nameof(MaintenanceMarketplaceValue));
        RaisePropertyChanged(nameof(MaintenanceMarketplaceDetail));
        RaisePropertyChanged(nameof(MaintenanceThemeValue));
        RaisePropertyChanged(nameof(MaintenanceThemeDetail));
        RaiseSupportBundlePreviewChanged();
        RaisePropertyChanged(nameof(HasConfigurationRecoveryNotice));
        RaisePropertyChanged(nameof(ConfigurationRecoveryTitle));
        RaisePropertyChanged(nameof(ConfigurationRecoveryDetail));
        RaisePropertyChanged(nameof(ProfileStatusLine));
        RaisePropertyChanged(nameof(WorkspaceRecommendationTitle));
        RaisePropertyChanged(nameof(WorkspaceRecommendationDetail));
        RaisePropertyChanged(nameof(WorkspaceRecommendationBrief));
        RaisePropertyChanged(nameof(MaintenanceGuidanceTitle));
        RaisePropertyChanged(nameof(MaintenanceGuidanceDetail));
        RaiseAutoReapplyStateChanged();
        RaisePropertyChanged(nameof(AccessPostureLabel));
        RaisePropertyChanged(nameof(RecommendedRunDuration));
        RaisePropertyChanged(nameof(RecommendedFollowUpText));
        RaisePropertyChanged(nameof(CustomRunReadinessTitle));
        RaisePropertyChanged(nameof(CustomRunReadinessDetail));
        RaisePropertyChanged(nameof(CustomApplyCaption));
        RebuildSelectionInsights();
    }

    private StackHealthComponent? HealthComponent(string id) =>
        HealthReport.Components.FirstOrDefault(component => string.Equals(component.Id, id, StringComparison.OrdinalIgnoreCase));

    private bool HasRecommendedAction(string action) =>
        HealthReport.Components.Any(component => component.RecommendedActionIds.Contains(action, StringComparer.Ordinal));

    private void RefreshMaintenanceActionRelevance()
    {
        foreach (var card in _maintenanceCards)
        {
            card.RefreshRelevance(IsMaintenanceActionRelevant(card.Action));
        }
    }

    private void RaiseMaintenanceActionCanExecuteChanged()
    {
        foreach (var card in _maintenanceCards)
        {
            card.Command.RaiseCanExecuteChanged();
        }
    }

    private void HandleAsyncCommandException(Exception ex)
    {
        if (ex is OperationCanceledException)
        {
            return;
        }

        AppendLog($"Desktop command failed: {ex.Message}", "ERROR");
        ShowNotice(
            "Action could not finish",
            ex.Message,
            "Review the run log before trying again.");
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
            "OpenMarketplace" => marketplace?.Severity == HealthSeverity.Ready,
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
            "Health report",
            true,
            true,
            "Required redacted health report, runtime metadata, and catalog pin baseline.",
            OnSupportBundleSelectionChanged));
        SupportBundleItems.Add(new SupportBundleCategoryViewModel(
            "operation",
            "Operation journal",
            false,
            true,
            "Recent install and watcher state from the LibreSpot profile.",
            OnSupportBundleSelectionChanged));
        SupportBundleItems.Add(new SupportBundleCategoryViewModel(
            "logs",
            "Logs",
            false,
            true,
            "Selected install, watcher, and desktop rolling logs.",
            OnSupportBundleSelectionChanged));
        SupportBundleItems.Add(new SupportBundleCategoryViewModel(
            "crashes",
            "Crash reports",
            false,
            true,
            "Newest crash report windows when present.",
            OnSupportBundleSelectionChanged));
    }

    private void OnSupportBundleSelectionChanged() => RefreshSupportBundlePreview();

    private SupportBundleOptions BuildSupportBundleOptions() =>
        new(
            IncludeOperationJournal: SupportBundleItems.FirstOrDefault(item => item.Id == "operation")?.IsSelected ?? true,
            IncludeLogs: SupportBundleItems.FirstOrDefault(item => item.Id == "logs")?.IsSelected ?? true,
            IncludeCrashReports: SupportBundleItems.FirstOrDefault(item => item.Id == "crashes")?.IsSelected ?? true);

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
        ExportSupportBundleCommand.RaiseCanExecuteChanged();
    }

    private void RaiseSupportBundlePreviewChanged()
    {
        RaisePropertyChanged(nameof(SupportBundlePreviewTitle));
        RaisePropertyChanged(nameof(SupportBundlePreviewDetail));
        RaisePropertyChanged(nameof(SupportBundleRedactionSummary));
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
            Title = "Export LibreSpot support bundle",
            Filter = "Zip archives (*.zip)|*.zip",
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
            SupportBundleLastExportText = $"Last export: {result.Path} ({FormatBytes(result.BytesWritten)}, {result.EntryCount} zip entries).";
            AppendLog($"Support bundle exported locally: {result.Path}", "SUCCESS");
        }
        catch (Exception ex)
        {
            SupportBundleLastExportText = $"Export failed: {ex.Message}";
            AppendLog($"Support bundle export failed: {ex.Message}", "ERROR");
        }
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
        RaisePropertyChanged(nameof(AutoReapplyStatusTitle));
        RaisePropertyChanged(nameof(AutoReapplyStatusDetail));
        RaisePropertyChanged(nameof(AutoReapplyTaskLine));
        RaisePropertyChanged(nameof(AutoReapplyLogLine));
        EnableAutoReapplyCommand.RaiseCanExecuteChanged();
        DisableAutoReapplyCommand.RaiseCanExecuteChanged();
    }

    private async Task ApplyRecommendedAsync()
    {
        var configuration = AppCatalog.CreateRecommendedConfiguration();
        configuration.Mode = "Easy";
        await StartBackendRunAsync(
            "Install",
            configuration,
            "Applying the recommended setup",
            "LibreSpot is rebuilding the tested SpotX and Spicetify stack with the default premium preset.",
            0);
    }

    private async Task ApplyCustomAsync()
    {
        var configuration = BuildConfiguration("Custom");
        await StartBackendRunAsync(
            "Install",
            configuration,
            "Applying your custom setup",
            "LibreSpot is validating your selections before it patches Spotify and restores the chosen visual stack.",
            1);
    }

    private Task RunMaintenanceAsync(MaintenanceActionDefinition definition)
    {
        var body = definition.IsDestructive
            ? $"{definition.Description}{Environment.NewLine}{Environment.NewLine}This is a deeper reset path and may remove the current customization state. Continue only when you are ready to rebuild."
            : $"{definition.Description}{Environment.NewLine}{Environment.NewLine}LibreSpot will keep this window open and stream backend progress while the action runs.";
        var (summaryTitle, summaryBody) = BuildMaintenancePromptSummary(definition);
        var requiresAdministrator = RequiresAdministrator(definition.Action);

        ShowPrompt(
            definition.Title,
            body,
            definition.ButtonText,
            definition.IsDestructive ? "Keep current setup" : "Cancel",
            definition.IsDestructive,
            () => StartBackendRunAsync(definition.Action, null, definition.Title, definition.Description, 2, requiresAdministrator),
            summaryTitle,
            summaryBody);

        return Task.CompletedTask;
    }

    private static bool RequiresAdministrator(string action) =>
        action is not ("CheckUpdates" or "CreateBackup" or "OpenMarketplace" or "RemoveSelfData");

    private void PresentAutoReapplyPrompt(bool enable)
    {
        if (IsRunning)
        {
            return;
        }

        var action = enable ? "EnableAutoReapply" : "DisableAutoReapply";
        var title = enable ? "Enable auto-reapply watcher" : "Disable auto-reapply watcher";
        var status = enable ? "Registering the auto-reapply watcher" : "Removing the auto-reapply watcher";
        var body = enable
            ? "LibreSpot will create a per-user scheduled task that runs after Windows sign-in and every 30 minutes. The watcher records the first Spotify version it sees, then reapplies your saved SpotX and Spicetify setup only after a version change while Spotify is closed."
            : "LibreSpot will remove the scheduled task and keep your saved profile untouched. Manual Reapply will still restore the same saved setup from Maintenance.";
        var summaryBody = enable
            ? "Creates LibreSpot\\ReapplyWatcher, writes watcher.log in the LibreSpot profile folder, and saves AutoReapply_Enabled in config.json."
            : "Deletes LibreSpot\\ReapplyWatcher if present and saves AutoReapply_Enabled=false in config.json.";

        ShowPrompt(
            title,
            body,
            enable ? "Enable watcher" : "Disable watcher",
            "Cancel",
            false,
            () => StartBackendRunAsync(action, null, title, status, 2, requiresAdministrator: false),
            "What this does",
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
            PresentAdministratorPrompt();
            return;
        }

        // First-run risk acknowledgment gate. Non-patching actions are exempt.
        if (action is not ("CheckUpdates" or "EnableAutoReapply" or "DisableAutoReapply"))
        {
            if (!await EnsureRiskAcknowledgedAsync())
            {
                return;
            }
        }

        SelectedWorkspaceIndex = targetWorkspaceIndex;
        ActivityTitle = title;
        ActivityStatus = status;
        ActivityStep = "Preparing backend runtime";
        ClearLog();
        ProgressValue = 0;
        IsActivityVisible = true;
        _runStopwatch.Restart();
        _runElapsedTimer.Start();
        RaisePropertyChanged(nameof(RunElapsedText));
        IsRunning = true;
        IsCancelRequested = false;

        _runCts?.Dispose();
        _runCts = new CancellationTokenSource();
        var token = _runCts.Token;

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
                    AppendLog("Configuration save was canceled.", "WARN");
                    ActivityStatus = "Canceled";
                    ActivityStep = "Configuration save canceled";
                    return;
                }
                catch (Exception ex)
                {
                    AppendLog($"Could not save configuration: {ex.Message}", "ERROR");
                    ActivityStatus = "Run needs attention";
                    ActivityStep = "Configuration save failed";
                    ProgressValue = 100;
                    return;
                }

                ApplyConfigurationToEditor(configuration);
            }

            var result = await _backendScriptService.RunAsync(action, _configurationService.ConfigPath, HandleBackendMessage, token);
            if (!result.Success)
            {
                AppendLog(result.ErrorMessage ?? "LibreSpot reported an unknown backend failure.", "ERROR");
                ActivityStatus = "Run needs attention";
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog("Backend run was canceled.", "WARN");
            ActivityStatus = "Canceled";
        }
        catch (Exception ex)
        {
            AppendLog($"Backend run failed: {ex.Message}", "ERROR");
            ActivityStatus = "Run needs attention";
        }
        finally
        {
            _runStopwatch.Stop();
            _runElapsedTimer.Stop();
            RaisePropertyChanged(nameof(RunElapsedText));
            IsRunning = false;
            IsCancelRequested = false;
            await RefreshSnapshotAsync();
        }
    }

    /// <summary>
    /// Requests cancellation of an in-flight backend run. Safe to call during window
    /// shutdown — if no run is active or the CTS has already been disposed this is a no-op.
    /// </summary>
    public void CancelRunningBackend()
    {
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
                        ActivityStatus = "Run complete";
                        ActivityStep = "LibreSpot is ready";
                        ProgressValue = 100;
                    }
                    else
                    {
                        ActivityStatus = "Run needs attention";
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
        if (string.IsNullOrEmpty(payload))
        {
            return;
        }

        LogEntries.Add(new LogEntryViewModel(DateTime.Now, level, payload));

        // Bound the collection so a very chatty backend can't pin memory or slow the UI.
        // The backend also writes every line to install.log on disk, so truncation here
        // is visual-only — diagnostics remain complete on disk.
        while (LogEntries.Count > MaxLogEntries)
        {
            LogEntries.RemoveAt(0);
        }

        CopyLogCommand.RaiseCanExecuteChanged();
        RaisePropertyChanged(nameof(LogLineCountText));
        RaisePropertyChanged(nameof(IsLogEmpty));
    }

    private void ClearLog()
    {
        LogEntries.Clear();
        CopyLogCommand.RaiseCanExecuteChanged();
        RaisePropertyChanged(nameof(LogLineCountText));
        RaisePropertyChanged(nameof(IsLogEmpty));
    }

    private async Task RefreshSnapshotAsync()
    {
        try
        {
            Snapshot = await _snapshotService.GetSnapshotAsync(_configurationService.ConfigPath);
            _snapshotRefreshedAt = DateTime.Now;
            RaisePropertyChanged(nameof(LastRefreshedText));
            RaiseSnapshotFreshnessChanged();
            RaiseSnapshotInsightsChanged();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Environment snapshot refresh failed");
        }
    }

    private void RaiseSnapshotFreshnessChanged()
    {
        RaisePropertyChanged(nameof(IsSnapshotStale));
        RaisePropertyChanged(nameof(SnapshotFreshnessTitle));
        RaisePropertyChanged(nameof(SnapshotFreshnessDetail));
    }

    private void PresentCancelRunPrompt()
    {
        if (!IsRunning || IsCancelRequested)
        {
            return;
        }

        ShowPrompt(
            "Cancel Current Run?",
            "LibreSpot will stop the backend process and keep the progress log collected so far." +
            Environment.NewLine + Environment.NewLine +
            "Partial changes may already exist, so reapplying afterward is usually the cleanest recovery path.",
            "Cancel run",
            "Keep running",
            true,
            () =>
            {
                IsCancelRequested = true;
                ActivityStatus = "Stopping backend…";
                ActivityStep = "Cancel requested";
                try { _runCts?.Cancel(); }
                catch (ObjectDisposedException) { }
                return Task.CompletedTask;
            },
            "If you stop here",
            "LibreSpot keeps the current log. Review it first, then rerun Recommended or Reapply if Spotify looks inconsistent.");
    }

    public void PresentCloseWhileRunningPrompt(Func<Task> confirmAction)
    {
        if (!IsRunning)
        {
            return;
        }

        ShowPrompt(
            "Close LibreSpot now?",
            "LibreSpot is still modifying Spotify." +
            Environment.NewLine + Environment.NewLine +
            "Closing now will stop the active backend run, dismiss the live progress shell, and leave any partial changes for a later reapply pass if you need one.",
            "Close and stop run",
            "Keep LibreSpot open",
            true,
            confirmAction,
            "Safer choice",
            "Keep LibreSpot open if you want the current backend step to finish and the final result to stay visible here.");
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

        // Clipboard is shared with other processes and can be briefly unavailable.
        // Try three times with a short yield before giving up so transient contention
        // (Office, clipboard managers, RDP) doesn't surface as a crash.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return;
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
                // Any other clipboard failure is non-fatal; the log text is also on disk.
                return;
            }
        }

        AppendLog("Clipboard was unavailable. Log is still saved to install.log.", "WARN");
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
            });
        }
        catch (Exception ex)
        {
            AppendLog($"Couldn't open the LibreSpot folder: {ex.Message}", "WARN");
        }
    }

    private void PresentAdministratorPrompt()
    {
        ShowPrompt(
            "Administrator permission required",
            "LibreSpot can open safely in standard mode, but setup and maintenance actions still need elevated access to modify Spotify, SpotX, and Spicetify files." +
            Environment.NewLine + Environment.NewLine +
            "Relaunching as administrator keeps the same desktop shell and unlocks apply, reapply, and reset actions.",
            "Relaunch as administrator",
            "Keep reviewing settings",
            false,
            () =>
            {
                RelaunchAsAdministrator();
                return Task.CompletedTask;
            },
            "Before you continue",
            "Nothing changes until Windows approves the relaunch. You can stay here and keep reviewing settings if you are not ready.");
    }

    private void RelaunchAsAdministrator()
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                ShowNotice("Unable to relaunch", "LibreSpot could not resolve its current executable path.", "Stay in standard mode");
                return;
            }

            // When running via `dotnet run`, ProcessPath points at dotnet.exe. Relaunching
            // that as admin would not start LibreSpot — warn instead of confusing the user.
            var exeName = Path.GetFileName(executablePath);
            if (string.Equals(exeName, "dotnet.exe", StringComparison.OrdinalIgnoreCase))
            {
                ShowNotice(
                    "Run LibreSpot.exe as administrator",
                    "LibreSpot is currently hosted by dotnet.exe (developer mode). Build a release binary and relaunch that as administrator.",
                    "Developer session");
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = true,
                Verb = "runas"
            };

            var process = Process.Start(startInfo);
            if (process is null)
            {
                ShowNotice("Relaunch failed", "The elevated process could not be started. Try right-clicking LibreSpot.exe and selecting 'Run as administrator' manually.", "Stay in standard mode");
                return;
            }
            Application.Current.Shutdown();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // 1223 = ERROR_CANCELLED. User clicked "No" on the UAC prompt — not an error.
            ShowNotice(
                "Administrator relaunch canceled",
                "LibreSpot is still open in standard mode. You can keep reviewing settings and relaunch when you are ready.",
                "Waiting for elevation");
        }
        catch (Exception ex)
        {
            AppendLog(ex.Message, "WARN");
            ShowNotice(
                "Unable to relaunch",
                "Windows did not start the elevated LibreSpot session. You can still close this window and run the app as administrator manually.",
                "Elevation failed");
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
        PromptTitle = title;
        PromptBody = body;
        PromptConfirmText = confirmText;
        PromptCancelText = cancelText;
        PromptSummaryTitle = summaryTitle ?? "What happens next";
        PromptSummaryBody = summaryBody ??
            (isDestructive
                ? "LibreSpot will make the requested change and leave the result visible here so you can review it afterward."
                : "LibreSpot will keep the window open, stream progress here, and leave the result easy to review afterward.");
        IsPromptDestructive = isDestructive;
        _pendingPromptAction = confirmAction;
        IsPromptVisible = true;
    }

    private async Task ConfirmPromptAsync()
    {
        var action = _pendingPromptAction;
        ClearPrompt();

        if (action is not null)
        {
            await action();
        }
    }

    private void CancelPrompt()
    {
        ClearPrompt();
    }

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

    private void ClearPrompt()
    {
        IsPromptVisible = false;
        PromptTitle = string.Empty;
        PromptBody = string.Empty;
        PromptConfirmText = "Continue";
        PromptCancelText = "Cancel";
        PromptSummaryTitle = string.Empty;
        PromptSummaryBody = string.Empty;
        IsPromptDestructive = false;
        _pendingPromptAction = null;
    }

    private static (string Title, string Body) BuildMaintenancePromptSummary(MaintenanceActionDefinition definition) =>
        definition.Action switch
        {
            "CheckUpdates" => ("What this does", "LibreSpot compares pinned versions plus the SpotX, Spicetify CLI, Marketplace, and themes compatibility matrix before you decide whether to update."),
            "Reapply" => ("What this does", "LibreSpot refreshes SpotX first, then restores the saved Spicetify layer so the stack returns to its last known profile."),
            "RepairMarketplace" => ("What this does", "LibreSpot reinstalls the Marketplace custom app, re-enables it in Spicetify, applies the change, and opens spotify:app:marketplace if Spotify accepts the URI."),
            "OpenMarketplace" => ("What this does", "LibreSpot asks Spotify to open spotify:app:marketplace without reinstalling or changing your Spicetify files."),
            "RestoreVanilla" => ("What this does", "This removes the visible Spicetify layer while leaving SpotX in place, so Spotify returns to a calmer default look."),
            "UninstallSpicetify" => ("What this removes", "LibreSpot restores Spotify first, then removes the Spicetify CLI, config folder, and PATH entry from this machine."),
            "FullReset" => ("What this removes", "LibreSpot clears Spotify customization state and related leftovers so the next install can start from a truly clean baseline."),
            _ => definition.IsDestructive
                ? ("What this removes", "LibreSpot will make a deeper cleanup pass and leave the result visible here afterward.")
                : ("What this does", "LibreSpot will keep the window open, stream progress here, and leave the result easy to review afterward.")
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
            "Risk acknowledgment",
            "LibreSpot modifies your Spotify installation to remove ads and apply themes. " +
            "This violates Spotify's Terms of Service and User Guidelines " +
            "(https://spotify.com/legal/user-guidelines/). " +
            "While enforcement against individual users has not been publicly documented, " +
            "your account could be affected." +
            Environment.NewLine + Environment.NewLine +
            "By continuing, you acknowledge this risk and agree to proceed at your own discretion." +
            Environment.NewLine + Environment.NewLine +
            "You can restore stock Spotify at any time using Maintenance > Full Reset.",
            "I understand, continue",
            "Cancel",
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
            "What this means",
            "LibreSpot will record your acknowledgment so this dialog does not appear again. " +
            "No data leaves your machine.");

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
        ActivityTitle = title;
        ActivityStatus = status;
        ActivityStep = step;
        ProgressValue = 0;
        IsActivityVisible = true;
        RaisePropertyChanged(nameof(RunElapsedText));
    }

    public void ApplyUiAutomationSmokeState(string state)
    {
        switch (state.Trim().ToLowerInvariant())
        {
            case "custom":
                SelectedWorkspaceIndex = 1;
                break;
            case "maintenance":
                SelectedWorkspaceIndex = 2;
                break;
            case "prompt":
                SelectedWorkspaceIndex = 0;
                ShowPrompt(
                    "UI automation prompt",
                    "This prompt is shown only for UI smoke coverage.",
                    "Confirm smoke action",
                    "Cancel smoke action",
                    false,
                    () => Task.CompletedTask,
                    "Smoke coverage",
                    "Confirms prompt labels, focus order, and modal boundaries without running an install.");
                break;
            case "activity":
                SelectedWorkspaceIndex = 0;
                AppendLog("UI automation smoke activity.", "INFO");
                ShowNotice(
                    "UI automation activity",
                    "Run complete",
                    "No install command was started.");
                ProgressValue = 100;
                break;
            default:
                SelectedWorkspaceIndex = 0;
                break;
        }
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private void RebuildSchemes()
    {
        SchemeOptions.Clear();
        if (AppCatalog.ThemeSchemes.TryGetValue(SelectedTheme, out var schemes))
        {
            foreach (var scheme in schemes)
            {
                SchemeOptions.Add(scheme);
            }
        }

        if (!SchemeOptions.Contains(SelectedScheme))
        {
            SelectedScheme = SchemeOptions.FirstOrDefault() ?? "Default";
        }
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
        configuration.Spicetify_Extensions = Extensions.Where(item => item.IsSelected).Select(item => item.Key).ToList();

        return AppCatalog.NormalizeConfiguration(configuration);
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

        var advancedCount = AdvancedOptions.Count(option => option.IsSelected);

        if (advancedCount == 0)
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "accent",
                "Conservative Core",
                "Advanced toggles are off, so this profile stays closer to the setup LibreSpot can reapply most predictably."));
        }
        else if (advancedCount <= 2)
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "accent",
                "Balanced Customization",
                "A few advanced tweaks are active, but the profile still reads like a deliberate daily-driver rather than an experiment bundle."));
        }
        else
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "warning",
                "Experimental Territory",
                "Several advanced options are enabled. Expect a more distinctive shell, with a little more maintenance after Spotify updates."));
        }

        if (HasConflictingSidebarOptions())
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "warning",
                "Right Sidebar Settings Overlap",
                "Hide right sidebar and clear right sidebar styling both target the same surface. Hiding the sidebar wins, so simplify this pair if you want a cleaner config."));
        }
        else if (!IsOptionSelected(nameof(InstallConfiguration.CleanInstall)) && Snapshot.SpotifyInstalled)
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "warning",
                "Overlay Install Selected",
                "LibreSpot will work on top of the current Spotify files. This is faster, but it leaves more room for older patch state to linger."));
        }
        else if (!IsOptionSelected(nameof(InstallConfiguration.CleanInstall)))
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "warning",
                "Skipping A Clean Start",
                "Because Spotify is not currently detected, overlay mode is unlikely to save time. A clean start is usually the calmer path."));
        }
        else
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "muted",
                "Fresh Baseline",
                "Clean install is on, so LibreSpot will clear more leftovers before rebuilding the stack."));
        }

        if (!IsLyricsThemeAvailable)
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "muted",
                "Lyrics Styling Is Parked",
                "The lyrics theme stays selected in your profile, but it will not apply until the lyrics patch is turned back on."));
        }
        else if (!IsThemeSchemeAvailable)
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "muted",
                "Marketplace-First Visual Stack",
                "You are skipping the theme pack, so LibreSpot will lean on Marketplace and SpotX presentation tweaks instead of a bundled skin."));
        }
        else
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "accent",
                "Theme Restore Ready",
                $"{SelectedTheme} with {Prettify.Label(SelectedScheme)} will be restored after the backend run completes."));
        }

        if (!IsOptionSelected(nameof(InstallConfiguration.Spicetify_Marketplace)) && SelectedExtensionLabels.Count == 0 && !IsThemeSchemeAvailable)
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "warning",
                "Minimal Spicetify Layer",
                "Marketplace, theme pack, and built-in extensions are all pared back. This keeps the shell lean, but removes most of LibreSpot's customization layer."));
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
                "Pinned Compatibility Target",
                CurrentSpotifyVersionEntry.Notes));
        }

        if (HasArchitectureMismatch)
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "warning",
                "Architecture Mismatch",
                ArchitectureMismatchWarning!));
        }

        if (!string.IsNullOrWhiteSpace(SelectedDownloadMethod))
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "muted",
                CurrentDownloadMethodEntry.Label,
                CurrentDownloadMethodEntry.Detail));
        }
    }

    private int CountProfileDifferencesFromRecommended()
    {
        var differences = EnumerateAllOptions().Count(option => option.IsSelected != option.IsRecommendedDefault);
        differences += Extensions.Count(extension => extension.IsSelected != extension.IsRecommendedDefault);

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
}
