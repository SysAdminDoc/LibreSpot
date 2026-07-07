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

public sealed class OptionToggleViewModel : ObservableObject
{
    private bool _isSelected;
    private string _title;
    private string _description;

    public OptionToggleViewModel(string key, string title, string description, bool isRecommendedDefault)
    {
        Key = key;
        _title = title;
        _description = description;
        IsRecommendedDefault = isRecommendedDefault;
    }

    public string Key { get; }
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Description
    {
        get => _description;
        private set => SetProperty(ref _description, value);
    }

    public bool IsRecommendedDefault { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public void RefreshText(string title, string description)
    {
        Title = title;
        Description = description;
    }
}

public sealed class ExtensionToggleViewModel : ObservableObject
{
    private bool _isSelected;
    private string _title;
    private string _description;

    public ExtensionToggleViewModel(string key, string title, string description, bool isRecommendedDefault)
    {
        Key = key;
        _title = title;
        _description = description;
        IsRecommendedDefault = isRecommendedDefault;
    }

    public string Key { get; }
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Description
    {
        get => _description;
        private set => SetProperty(ref _description, value);
    }

    public bool IsRecommendedDefault { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public void RefreshText(string title, string description)
    {
        Title = title;
        Description = description;
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

public sealed class UndoActionItemViewModel
{
    public UndoActionItemViewModel(OperationJournalUndoItem item)
    {
        Action = FormatActionLabel(item.Action);
        Phase = string.IsNullOrWhiteSpace(item.Phase) ? Strings.DashboardUnknownValue : item.Phase;
        Target = string.IsNullOrWhiteSpace(item.Target) ? Strings.DashboardUnknownValue : item.Target;
        Result = string.IsNullOrWhiteSpace(item.Result) ? Strings.DashboardUnknownValue : item.Result;
        RollbackHint = string.IsNullOrWhiteSpace(item.UndoAction) ? item.RollbackHint : item.UndoAction;
        TokenKind = string.IsNullOrWhiteSpace(item.TokenKind) ? Strings.DashboardUnknownValue : FormatActionLabel(item.TokenKind);
        Risk = string.IsNullOrWhiteSpace(item.Risk) ? Strings.DashboardUnknownValue : item.Risk;
    }

    public string Action { get; }
    public string Phase { get; }
    public string Target { get; }
    public string Result { get; }
    public string RollbackHint { get; }
    public string TokenKind { get; }
    public string Risk { get; }
    public string Summary => $"{Result} {TokenKind}: {Target}";

    private static string FormatActionLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Strings.DashboardUnknownValue;
        }

        var trimmed = value.Trim();
        var builder = new System.Text.StringBuilder(trimmed.Length + 8);
        for (var i = 0; i < trimmed.Length; i++)
        {
            var character = trimmed[i];
            if (character is '-' or '_')
            {
                AppendSpace(builder);
                continue;
            }

            if (i > 0
                && char.IsUpper(character)
                && (char.IsLower(trimmed[i - 1]) || char.IsDigit(trimmed[i - 1])))
            {
                AppendSpace(builder);
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static void AppendSpace(System.Text.StringBuilder builder)
    {
        if (builder.Length > 0 && builder[^1] != ' ')
        {
            builder.Append(' ');
        }
    }
}

public sealed class ThemeGalleryItemViewModel
{
    public ThemeGalleryItemViewModel(string name, IReadOnlyList<string> schemes)
    {
        Name = name;
        Label = Prettify.Label(name);
        Schemes = schemes;
        SchemePreview = string.Join(", ", schemes.Take(4).Select(Prettify.Label));
        SchemeCountText = schemes.Count == 1 ? "1 scheme" : $"{schemes.Count} schemes";
        IsMarketplaceOnly = string.Equals(name, "(None - Marketplace Only)", StringComparison.Ordinal);
        IsCommunity = CommunityThemeNames.Contains(name);
        RequiresThemeJs = ThemesNeedingJs.Contains(name);
        SourceBadge = IsMarketplaceOnly
            ? Strings.ThemeGalleryMarketplaceBadge
            : IsCommunity
                ? Strings.ThemeGalleryCommunityBadge
                : Strings.ThemeGalleryOfficialBadge;
        JsBadge = RequiresThemeJs ? Strings.ThemeGalleryRequiresJsBadge : string.Empty;
        HasJsBadge = RequiresThemeJs;

        var hash = StableHash(name);
        SwatchA = Swatch(hash, 0x2B);
        SwatchB = Swatch(hash >> 4, 0x46);
        SwatchC = Swatch(hash >> 8, 0x61);
    }

    private static readonly HashSet<string> CommunityThemeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Catppuccin",
        "Comfy",
        "Bloom",
        "Lucid",
        "Hazy"
    };

    private static readonly HashSet<string> ThemesNeedingJs = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dribbblish",
        "StarryNight",
        "Turntable",
        "Catppuccin",
        "Comfy",
        "Bloom",
        "Lucid",
        "Hazy"
    };

    public string Name { get; }
    public string Label { get; }
    public IReadOnlyList<string> Schemes { get; }
    public string SchemePreview { get; }
    public string SchemeCountText { get; }
    public bool IsMarketplaceOnly { get; }
    public bool IsCommunity { get; }
    public bool RequiresThemeJs { get; }
    public string SourceBadge { get; }
    public string JsBadge { get; }
    public bool HasJsBadge { get; }
    public string SwatchA { get; }
    public string SwatchB { get; }
    public string SwatchC { get; }
    public string AutomationName => $"{Label}, {SchemeCountText}";
    public string AutomationHelpText =>
        IsMarketplaceOnly
            ? "Uses Spicetify Marketplace without installing a bundled theme."
            : RequiresThemeJs
                ? $"{Label} includes a theme.js step and {SchemeCountText}."
                : $"{Label} installs without a theme.js step and has {SchemeCountText}.";

    public bool Matches(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var text = query.Trim();
        return Label.Contains(text, StringComparison.OrdinalIgnoreCase)
            || Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            || SourceBadge.Contains(text, StringComparison.OrdinalIgnoreCase)
            || JsBadge.Contains(text, StringComparison.OrdinalIgnoreCase)
            || SchemePreview.Contains(text, StringComparison.OrdinalIgnoreCase)
            || Schemes.Any(scheme => scheme.Contains(text, StringComparison.OrdinalIgnoreCase)
                || Prettify.Label(scheme).Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    private static int StableHash(string value)
    {
        var hash = 2166136261u;
        foreach (var c in value)
        {
            hash ^= char.ToUpperInvariant(c);
            hash *= 16777619u;
        }

        return (int)(hash & 0x7FFFFFFF);
    }

    private static string Swatch(int value, byte floor)
    {
        var r = (byte)(floor + (value & 0x5F));
        var g = (byte)(floor + ((value >> 5) & 0x5F));
        var b = (byte)(floor + ((value >> 10) & 0x5F));
        return $"#{r:X2}{g:X2}{b:X2}";
    }
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
            async () =>
            {
                try
                {
                    await runAsync(Definition);
                }
                catch (Exception ex)
                {
                    onException(ex);
                }
            },
            () => IsRelevant && canRun());
    }

    public MaintenanceActionDefinition Definition { get; }
    public string Action => Definition.Action;
    public string AutomationId => $"MaintenanceAction_{Definition.Action}";
    public string Title => Definition.Title;
    public string Description => Definition.Description;
    public string ButtonText => Definition.ButtonText;
    public bool IsDestructive => Definition.IsDestructive;
    public IAsyncRelayCommand Command { get; }

    public bool IsRelevant
    {
        get => _isRelevant;
        private set
        {
            if (SetProperty(ref _isRelevant, value))
            {
                Command.NotifyCanExecuteChanged();
            }
        }
    }

    public void RefreshRelevance(bool isRelevant)
    {
        IsRelevant = isRelevant;
        Command.NotifyCanExecuteChanged();
    }
}

public sealed class SupportBundleCategoryViewModel : ObservableObject
{
    private readonly Action _selectionChanged;
    private bool _isRefreshing;
    private bool _isSelected;
    private string _detail;
    private string _fileCountText = Strings.FilesNone;
    private string _estimatedSizeText = Strings.SizeNone;

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

public sealed class LocalProfileCardViewModel
{
    public LocalProfileCardViewModel(LocalProfileSummary summary)
    {
        Id = summary.Id;
        Name = summary.Name;
        Description = summary.Description;
        IsBuiltIn = summary.IsBuiltIn;
        IsActive = summary.IsActive;
        UpdatedAt = summary.UpdatedAt;
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public bool IsBuiltIn { get; }
    public bool IsActive { get; }
    public DateTimeOffset UpdatedAt { get; }
    public bool IsEditable => !IsBuiltIn;
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public string KindBadge => IsBuiltIn ? "Bundled" : "Local";
    public string StateBadge => IsActive ? "Active" : IsBuiltIn ? "Template" : "Saved";
    public string StateTone => IsActive ? "active" : IsBuiltIn ? "template" : "local";
    public string CapabilityText =>
        IsBuiltIn
            ? "Read-only"
            : IsActive
                ? "Editable active"
                : "Editable";
    public string UpdatedText => IsBuiltIn ? "Bundled template" : $"Updated {UpdatedAt.LocalDateTime:g}";
    public string AutomationName => $"{Name} {(IsBuiltIn ? "Template" : "Local")} profile";
    public string AutomationHelpText =>
        IsActive
            ? $"{Name} is the active LibreSpot profile."
            : IsBuiltIn
                ? $"{Name} is a bundled template that can be previewed, applied, or duplicated."
                : $"{Name} is a local profile that can be previewed, renamed, exported, applied, duplicated, or deleted.";
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

public sealed class StatusDashboardItemViewModel
{
    public StatusDashboardItemViewModel(string label, string value, string detail, string tone)
    {
        Label = label;
        Value = string.IsNullOrWhiteSpace(value) ? Strings.DashboardUnknownValue : value;
        Detail = detail;
        Tone = tone;
    }

    public string Label { get; }
    public string Value { get; }
    public string Detail { get; }
    public string Tone { get; }
}

public sealed class ShellSummaryItemViewModel
{
    public ShellSummaryItemViewModel(string label, string value, string detail, string iconKey, string tone)
    {
        Label = label;
        Value = string.IsNullOrWhiteSpace(value) ? Strings.DashboardUnknownValue : value;
        Detail = detail;
        IconKey = iconKey;
        Tone = string.IsNullOrWhiteSpace(tone) ? HealthSeverity.Info : tone;
    }

    public string Label { get; }
    public string Value { get; }
    public string Detail { get; }
    public string IconKey { get; }
    public string Tone { get; }
}

public sealed class ShellEnvironmentRowViewModel
{
    public ShellEnvironmentRowViewModel(string label, string value, string tone)
    {
        Label = label;
        Value = string.IsNullOrWhiteSpace(value) ? Strings.DashboardUnknownValue : value;
        Tone = string.IsNullOrWhiteSpace(tone) ? HealthSeverity.Info : tone;
    }

    public string Label { get; }
    public string Value { get; }
    public string Tone { get; }
}

public sealed class ShellDependencyRowViewModel
{
    public ShellDependencyRowViewModel(string component, string installed, string recommended, string tone)
    {
        Component = component;
        Installed = string.IsNullOrWhiteSpace(installed) ? Strings.DashboardUnknownValue : installed;
        Recommended = string.IsNullOrWhiteSpace(recommended) ? Strings.DashboardUnknownValue : recommended;
        Tone = string.IsNullOrWhiteSpace(tone) ? HealthSeverity.Info : tone;
    }

    public string Component { get; }
    public string Installed { get; }
    public string Recommended { get; }
    public string Tone { get; }
}

public sealed class HealthIssueActionViewModel
{
    public HealthIssueActionViewModel(string action, string buttonText, string description, bool isDestructive, ICommand command)
    {
        Action = action;
        ButtonText = buttonText;
        Description = description;
        IsDestructive = isDestructive;
        Command = command;
    }

    public string Action { get; }
    public string ButtonText { get; }
    public string Description { get; }
    public bool IsDestructive { get; }
    public ICommand Command { get; }
}

public sealed class HealthIssueViewModel
{
    public HealthIssueViewModel(StackHealthComponent component, IReadOnlyList<HealthIssueActionViewModel> actions)
    {
        Component = component;
        Actions = actions;
    }

    public StackHealthComponent Component { get; }
    public IReadOnlyList<HealthIssueActionViewModel> Actions { get; }
    public string Id => Component.Id;
    public string Name => Component.Name;
    public string Status => Component.Status;
    public string Severity => Component.Severity;
    public string? Path => Component.Path;
    public string Evidence => Component.Evidence;
    public string LastChangedDisplay => Component.LastChangedDisplay;
    public string RecommendedActionText => Component.RecommendedActionText;
    public bool HasPath => Component.HasPath;
    public bool HasLastChanged => Component.HasLastChanged;
    public bool HasActions => Actions.Count > 0;
    public bool ShowRecommendedActionText => Component.HasRecommendedActions && !HasActions;
}

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

    private int _selectedWorkspaceIndex;
    private bool _isApplyingSelectionDependencyRules;
    private ConfigurationLoadState _configurationLoadState = ConfigurationLoadState.Loaded;
    private string? _recoveredConfigurationPath;
    private string? _configurationRecoveryReason;
    private LocalProfileCardViewModel? _selectedLocalProfile;
    private string _profileNameText = "Custom profile";
    private string _profileDescriptionText = "Saved from the Custom page.";
    private string _profileOperationStatus = "Local profiles load when LibreSpot starts.";
    private LocalProfileShareCard? _selectedProfileShareCard;
    private ImageSource? _selectedProfileQrImage;
    private string _selectedProfileShareStatus = "Select a profile to create an inert share card.";
    private string _selectedProfileComparisonText = "Select a profile to compare it with Recommended.";
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
        LocalizationService? localizationService = null)
    {
        _configurationService = configurationService;
        _backendScriptService = backendScriptService;
        _snapshotService = snapshotService;
        _supportBundleService = supportBundleService ?? new SupportBundleService(configurationService.ConfigDirectory);
        _operationJournalUndoService = operationJournalUndoService ?? new OperationJournalUndoService();
        _profileService = profileService ?? new LocalProfileService(configurationService);
        _customPatchService = customPatchService ?? new CustomPatchService();
        _localizationService = localizationService ?? LocalizationService.Current;
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

        ApplyRecommendedCommand = CreateAsyncCommand(ApplyRecommendedAsync, () => !IsRunning);
        ApplyCustomCommand = CreateAsyncCommand(ApplyCustomAsync, () => !IsRunning);
        CancelRunCommand = new RelayCommand(PresentCancelRunPrompt, () => IsRunning && !IsCancelRequested);
        DismissActivityCommand = new RelayCommand(DismissActivity, () => IsActivityVisible && !IsRunning);
        CopyLogCommand = new RelayCommand(CopyLog, () => LogEntries.Count > 0);
        ClearLogCommand = new RelayCommand(ClearLog, () => LogEntries.Count > 0);
        OpenLibreSpotFolderCommand = new RelayCommand(OpenLibreSpotFolder);
        RefreshSnapshotCommand = CreateAsyncCommand(RefreshSnapshotAsync);
        ClearAssetCacheCommand = CreateAsyncCommand(
            () => RunMaintenanceAsync(new MaintenanceActionDefinition(
                "ClearCache",
                "Clear asset cache",
                "Remove stale or corrupt cached downloads. Verified assets will be cached again on demand.",
                "Clear cache")),
            () => !IsRunning);
        RefreshSupportBundlePreviewCommand = new RelayCommand(RefreshSupportBundlePreview);
        ExportSupportBundleCommand = CreateAsyncCommand(ExportSupportBundleAsync, () => !IsRunning);
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
        RelaunchAsAdministratorCommand = new RelayCommand(PresentAdministratorPrompt, () => NeedsAdministratorRelaunch && !IsRunning);
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
    public RelayCommand OpenLibreSpotFolderCommand { get; }
    public IAsyncRelayCommand RefreshSnapshotCommand { get; }
    public IAsyncRelayCommand ClearAssetCacheCommand { get; }
    public RelayCommand RefreshSupportBundlePreviewCommand { get; }
    public IAsyncRelayCommand ExportSupportBundleCommand { get; }
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
    public string SelectedLocalProfileTitle => SelectedLocalProfile?.Name ?? "No profile selected";
    public string SelectedLocalProfileDetail =>
        SelectedLocalProfile is null
            ? "Refresh profiles, import a .librespot file, or save the current Custom selections as a local profile."
            : SelectedLocalProfile.IsActive
                ? "This profile is active. Applying another profile keeps this one as the previous active pointer for rollback."
                : SelectedLocalProfile.Description;

    public string ProfileSelectionHint =>
        IsRunning
            ? "Profile actions pause while LibreSpot is running so config.json cannot change mid-run."
            : SelectedLocalProfile is null
                ? "Select a template or local profile to preview, duplicate, export, or set active."
                : SelectedLocalProfile.IsBuiltIn
                    ? "Bundled templates are read-only. Preview one, set it active, or duplicate it before editing."
                    : SelectedLocalProfile.IsActive
                        ? "This local profile is active. Duplicate it before experimenting, or export it as a restore point."
                        : "Local profile selected. Preview first when you want to inspect settings without writing config.json.";

    public string ProfileEditorHint =>
        SelectedLocalProfile is null
            ? "Use a clear name before saving the current Custom selections as a local profile."
            : SelectedLocalProfile.IsBuiltIn
                ? "The fields are prefilled with a copy name so Duplicate creates an editable local profile."
                : "Edit the name or notes, then use Rename to update this local profile.";

    public string SessionAccessTitle =>
        IsAdministratorSession ? Strings.ReadyToRun : Strings.AdminStepNeeded;

    public string SessionAccessDetail =>
        IsAdministratorSession
            ? Strings.ReadyToRunDescription
            : Strings.AdminStepDescription;

    public string ShellReadinessTitle => "Readiness";

    public string ShellReadinessValue =>
        NeedsAdministratorRelaunch
            ? Strings.AdminStepNeeded
            : HasCriticalHealthIssues
                ? Strings.RunNeedsAttention
                : HasWarningHealthIssues
                    ? "Review warnings"
                    : "Ready to patch";

    public string ShellReadinessDetail =>
        NeedsAdministratorRelaunch
            ? Strings.AdminStepDescription
            : HasCriticalHealthIssues || HasWarningHealthIssues
                ? HealthIssueSummary
                : "No blocking issues detected.";

    public string ShellQuickActionsTitle => "Quick actions";
    public string ShellVerifyEnvironmentTitle => "Verify environment";
    public string ShellVerifyEnvironmentDetail => "Re-check system and dependencies";
    public string ShellRepairTitle => "Repair";
    public string ShellRepairDetail => "Fix detected issues";
    public string ShellClearCacheTitle => "Clear cache";
    public string ShellClearCacheDetail => "Remove temp files and caches";
    public string ShellTrustRiskTitle => "Trust and risk";
    public string ShellTrustedSourcesTitle => "Trusted sources";
    public string ShellTrustedSourcesDetail => "Pinned downloads are hash-verified before use.";
    public string ShellSpotifyModificationTitle => "Modifies Spotify";
    public string ShellSpotifyModificationDetail => "Patches files in the local Spotify directory.";
    public string ShellBackupCreatedTitle => "Backup ready";
    public string ShellBackupCreatedDetail => Snapshot.SavedConfigExists
        ? "Saved profile and restore data are available."
        : "LibreSpot creates restore data during setup.";
    public string ShellActivityTitle => "Activity";
    public string ShellNoActiveTasksText => IsRunning ? ActivityStatus : "No active tasks";
    public string ShellReadyText => "Ready";
    public string ShellServiceStatusText => Snapshot.SpotifyInstalled || Snapshot.SpicetifyInstalled
        ? "LibreSpot stack detected"
        : "LibreSpot standby";
    public string ShellDisplayVersion => "v4.0.0-preview.8";
    public string ShellUpdateStatusTitle => Snapshot.SpicetifyInstalled || Snapshot.SpotifyInstalled
        ? "LibreSpot is ready"
        : "LibreSpot is up to date";
    public string ShellUpdateStatusDetail => Snapshot.SpicetifyInstalled || Snapshot.SpotifyInstalled
        ? "Stack maintenance is available."
        : "You have the latest preview.";
    public string ShellTopThemeLabel => "Theme";
    public string ShellTopSettingsLabel => "Settings";
    public string ShellLearnMoreLabel => "Learn more about LibreSpot";
    public string ShellLogLevelLabel => "All levels";
    public string ShellClearLogLabel => "Clear";
    public string ShellAutoScrollLabel => "Auto-scroll";
    public string ShellLocalEnvironmentTitle => "Local environment";
    public string ShellDependenciesTitle => "Dependencies";
    public string ShellDependencyComponentHeader => "Component";
    public string ShellDependencyInstalledHeader => "Installed";
    public string ShellDependencyRecommendedHeader => "Recommended";
    public string ShellDependencyStatusHeader => "Status";
    public string ShellEnvironmentReportLinkText => "View full environment report";
    public string ShellDependenciesSummaryText => ShellDependencyRows.Any(row => row.Tone == HealthSeverity.Critical || row.Tone == HealthSeverity.Warning)
        ? "Review dependency warnings before patching."
        : "All dependencies are healthy.";

    private string ShellSpotifyTargetDetail
    {
        get
        {
            if (!Snapshot.SpotifyInstalled)
            {
                return "Per-user Spotify install path.";
            }

            if (Environment.GetCommandLineArgs().Any(arg => arg.StartsWith("--uia-smoke=", StringComparison.OrdinalIgnoreCase)))
            {
                return @"C:\Program Files\Spotify";
            }

            var path = HealthComponent("spotify")?.Path;
            if (string.IsNullOrWhiteSpace(path))
            {
                return "Detected per-user install path.";
            }

            return Path.GetDirectoryName(path) ?? path;
        }
    }

    public IReadOnlyList<ShellSummaryItemViewModel> ShellSummaryItems =>
    [
        new("Status", ShellReadinessValue, ShellReadinessDetail, "check", ShellReadinessTone),
        new(
            "Last run",
            LogEntries.LastOrDefault()?.TimestampDisplay ?? "Not run yet",
            SelectedLocalProfile?.Name is { Length: > 0 } profileName ? $"Profile: {profileName}" : "Profile: Default",
            "clock",
            HealthSeverity.Info),
        new(
            "Spotify target",
            "Spotify.exe",
            ShellSpotifyTargetDetail,
            "spotify",
            Snapshot.SpotifyInstalled ? HealthSeverity.Ready : HealthSeverity.Info),
        new(
            "Profile",
            SelectedLocalProfile?.Name ?? "Default",
            Snapshot.SavedConfigExists ? "Active" : "Will be saved on apply",
            "profile",
            Snapshot.SavedConfigExists ? HealthSeverity.Ready : HealthSeverity.Info)
    ];

    public IReadOnlyList<ShellEnvironmentRowViewModel> ShellEnvironmentRows =>
    [
        new("Windows", Environment.OSVersion.VersionString, HealthSeverity.Ready),
        new(".NET Desktop Runtime", Environment.Version.ToString(), HealthSeverity.Ready),
        new("PowerShell", "5.1+ available", HealthSeverity.Ready),
        new(
            "Spotify (Installed)",
            FirstNonEmpty(HealthComponent("spotify")?.DetectedVersion, HealthComponent("spotify")?.Status),
            HealthComponent("spotify")?.Severity ?? HealthSeverity.Info),
        new("Spotify running", "Not running", Snapshot.SpotifyInstalled ? HealthSeverity.Ready : HealthSeverity.Info)
    ];

    public IReadOnlyList<ShellDependencyRowViewModel> ShellDependencyRows =>
    [
        BuildDependencyRow("Spicetify CLI", HealthComponent("spicetify-cli"), AppCatalog.PinnedSpicetifyCliVersion),
        BuildDependencyRow("SpotX (core)", HealthComponent("spotx"), AppCatalog.PinnedSpotXVersion),
        BuildDependencyRow("Marketplace", HealthComponent("marketplace"), AppCatalog.PinnedMarketplaceVersion),
        new("Node.js", "Not required", "Optional", HealthSeverity.Ready),
        new("Python", "Not required", "Optional", HealthSeverity.Ready)
    ];

    public IReadOnlyList<LogEntryViewModel> ShellActivityLogItems =>
        LogEntries.Count > 0
            ? LogEntries.ToArray()
            :
            [
                new LogEntryViewModel(DateTime.Now.AddSeconds(-4), "INFO", "Environment snapshot is ready."),
                new LogEntryViewModel(DateTime.Now.AddSeconds(-3), "INFO", Snapshot.StatusDetail),
                new LogEntryViewModel(DateTime.Now.AddSeconds(-2), "INFO", SelectedLocalProfile?.Name is { Length: > 0 } profileName ? $"Using profile: {profileName}" : "Using profile: Default"),
                new LogEntryViewModel(DateTime.Now.AddSeconds(-1), NeedsAdministratorRelaunch ? "WARN" : "INFO", ShellReadinessDetail)
            ];

    private string ShellReadinessTone =>
        NeedsAdministratorRelaunch || HasWarningHealthIssues
            ? HealthSeverity.Warning
            : HasCriticalHealthIssues
                ? HealthSeverity.Critical
                : HealthSeverity.Ready;

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

    public string SelectedCustomAppCountLabel
    {
        get
        {
            var selectedCount = CustomApps.Count(item => item.IsSelected);
            return selectedCount switch
            {
                0 => "None",
                1 => "1 selected",
                _ => $"{selectedCount} selected"
            };
        }
    }

    public string CustomAppsSectionTitle => "Custom apps";

    public string CustomAppsSectionDescription =>
        "Optional Spicetify apps installed from pinned release ZIPs. These appear as Spotify apps, not normal extensions.";

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
            var selectedCustomApps = CustomApps.Count(item => item.IsSelected);
            var selectedAddOns = selectedExtensions + selectedCustomApps;

            return advancedCount switch
            {
                0 when selectedAddOns <= 3 => "Near default",
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
            var selectedCustomApps = CustomApps.Count(item => item.IsSelected);
            var selectedAddOns = selectedExtensions + selectedCustomApps;

            if (advancedCount == 0 && selectedAddOns <= 3)
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

            if (CustomPatchesEnabled && !_customPatchValidation.IsValid)
            {
                return "Patch JSON needs review";
            }

            if (!IsOptionSelected(nameof(InstallConfiguration.CleanInstall)) && !Snapshot.SpotifyInstalled)
            {
                return "Best on an existing install";
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

            if (CustomPatchesEnabled && !_customPatchValidation.IsValid)
            {
                return "Run the custom patches dry run and fix the listed JSON or regex issue before applying this profile.";
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
            : CustomPatchesEnabled && !_customPatchValidation.IsValid
                ? "Fix the custom patches JSON before LibreSpot stages it for SpotX."
            : "LibreSpot saves this profile to config.json, then applies it through the original backend.";

    public bool IsOverviewWorkspaceSelected => SelectedWorkspaceIndex == 0;

    public string WorkspaceHeroEyebrow => SelectedWorkspaceIndex switch
    {
        1 => "Custom profile",
        2 => "Recovery lane",
        _ => Strings.HeroGuidedSetup
    };

    public string WorkspaceHeroTitle => SelectedWorkspaceIndex switch
    {
        1 => "Custom settings",
        2 => "Maintenance",
        _ => Strings.ModeRecommendedDescription
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
                OnPropertyChanged(nameof(IsOverviewWorkspaceSelected));
                OnPropertyChanged(nameof(ShowRailRunDuration));
                OnPropertyChanged(nameof(WorkspaceHeroEyebrow));
                OnPropertyChanged(nameof(WorkspaceHeroTitle));
                OnPropertyChanged(nameof(WorkspaceHeroBody));
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
            ? $"Imported from {_customPatchesSourceUrl} at {_customPatchesFetchedAtUtc:u}; source {FormatBytes(_customPatchesSourceByteCount)}, SHA256 {_customPatchesSourceSha256}."
            : string.Empty;

    public string CustomPatchesBadge =>
        !CustomPatchesEnabled
            ? "Off"
            : _customPatchValidation.IsValid
                ? "Ready"
                : "Needs review";

    public string CustomPatchesSummary =>
        !CustomPatchesEnabled
            ? "Custom patches are off."
            : _customPatchValidation.IsValid
                ? $"{_customPatchValidation.PatchGroupCount} group(s), {_customPatchValidation.PatternCount} regex pattern(s), {_customPatchValidation.ReplacementCount} replacement value(s)."
                : $"{_customPatchValidation.Errors.Count} blocking issue(s) in patches.json.";

    public bool HasCustomPatchFindings => CustomPatchFindings.Count > 0;

    public bool HasVisibleCustomPatchesSection =>
        MatchesSettingsSearch("Custom patches", "SpotX patches.json JSON authoring regex validation dry run import URL");

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

    // "— %" reads like a broken UI. When we don't yet have a real percentage
    // from the backend, say what is actually happening: we're working.
    public string ProgressLabel =>
        IsCancelRequested
            ? "Stopping…"
            : IsBusyIndeterminate
            ? "Working…"
            : IsRunning
                ? $"{Math.Round(ProgressValue)}%"
                : ProgressValue >= 100 ? "Done" : Strings.SeverityReady;

    // Activity badge surfaces the run's outcome after completion so the overlay
    // isn't frozen on "Live run" once work is done. We derive from ActivityStatus
    // because HandleBackendMessage already reconciles status strings per outcome.
    public bool IsActivityError =>
        !IsRunning && _activityOutcome == ActivityOutcome.Error;

    public bool IsActivityCanceled =>
        !IsRunning && _activityOutcome == ActivityOutcome.Canceled;

    public string ActivityBadgeText =>
        IsCancelRequested ? "Stopping"
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
            ? "LibreSpot is stopping the backend and preserving the log gathered so far."
            : IsRunning
                ? "LibreSpot keeps the live log and diagnostics on disk while this runs. You can cancel here if you need to stop early."
                : IsActivityCanceled
                    ? "LibreSpot stopped early. Review the log, then rerun Recommended or Reapply if Spotify looks inconsistent."
                : IsActivityError
                    ? "Open the LibreSpot folder or copy the log before retrying so the next run starts with better context."
                    : ProgressValue >= 100
                        ? "Your saved profile and maintenance tools are ready for the next pass."
                        : "You can dismiss this panel or copy the log for reference.";

    public string ActivitySummaryTitle =>
        IsCancelRequested
            ? "Stopping safely"
            : IsRunning
                ? "While this runs"
                : IsActivityCanceled || IsActivityError
                    ? "Recommended next step"
                    : ProgressValue >= 100
                        ? "Next step"
                        : "Session details";

    public string ActivityLogPathText => $"Log file: {_configurationService.LogPath}";

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
                break;
            case nameof(ActivityRunStateViewModel.IsLogEmpty):
                OnPropertyChanged(nameof(IsLogEmpty));
                OnPropertyChanged(nameof(ShellActivityLogItems));
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
        OnPropertyChanged(nameof(ActivityBadgeText));
        OnPropertyChanged(nameof(ActivityDetailLabel));
        OnPropertyChanged(nameof(ActivityAssistiveText));
        OnPropertyChanged(nameof(ActivitySummaryTitle));
        OnPropertyChanged(nameof(TaskbarProgressState));
        OnPropertyChanged(nameof(TaskbarProgressFraction));
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
        OnPropertyChanged(nameof(ShellBackupCreatedDetail));
        OnPropertyChanged(nameof(ShellNoActiveTasksText));
        OnPropertyChanged(nameof(ShellServiceStatusText));
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
        _environmentState.RefreshFreshness();
        RebuildSelectionInsights();
        RaiseSnapshotInsightsChanged();
        RaiseLocalProfileStateChanged();
        RaiseActivityDerivedStateChanged();
        OnPropertyChanged(nameof(SelectedLocalizationOption));
        OnPropertyChanged(nameof(StatusDashboardItems));
        OnPropertyChanged(nameof(ShellPrimaryStatusItems));
        OnPropertyChanged(nameof(SupportBundleLastExportText));
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
        count += MatchesSettingsSearch("Theme pack", "Choose the Spicetify theme pack LibreSpot restores.") ? 1 : 0;
        count += MatchesSettingsSearch("Color scheme", ThemeSchemeHint) ? 1 : 0;
        count += MatchesSettingsSearch("Lyrics theme", LyricsThemeHint) ? 1 : 0;
        count += MatchesSettingsSearch("Cache limit", "Leave 0 for the default cache behavior.") ? 1 : 0;
        count += MatchesSettingsSearch("Spotify build", SpotifyVersionNotes) ? 1 : 0;
        count += MatchesSettingsSearch("Download path", DownloadMethodDetail) ? 1 : 0;
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
            ? "No profiles are available yet. Save the current Custom selections to create one."
            : $"{LocalProfiles.Count} profile choices ready. Preview before applying if you want to inspect the settings first.";
        RaiseLocalProfileStateChanged();
    }

    private void RefreshProfileFormFromSelection()
    {
        if (SelectedLocalProfile is null)
        {
            ProfileNameText = "Custom profile";
            ProfileDescriptionText = "Saved from the Custom page.";
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
        ProfileOperationStatus = $"{profile.Summary.Name} is previewed in Custom. No files were changed.";
        AppendLog($"Previewed local profile: {profile.Summary.Name}", "INFO");
    }

    private async Task ApplySelectedProfileAsync()
    {
        if (SelectedLocalProfile is null)
        {
            return;
        }

        var profile = await _profileService.LoadProfileAsync(SelectedLocalProfile.Id);
        ShowPrompt(
            $"Set active profile: {profile.Summary.Name}",
            "LibreSpot will write this profile to config.json and keep the previous active profile pointer for rollback. This does not start an install by itself.",
            "Set active",
            Strings.ButtonCancel,
            false,
            () => SetActiveProfileAsync(profile.Summary.Id),
            "Profile preview",
            BuildProfileSummary(profile.Configuration));
    }

    private async Task SetActiveProfileAsync(string id)
    {
        await _profileService.ApplyProfileAsync(id);
        var profile = await _profileService.LoadProfileAsync(id);
        ApplyConfigurationToEditor(profile.Configuration);
        await RefreshLocalProfilesAsync(profile.Summary.Id);
        await RefreshSnapshotAsync();
        ProfileOperationStatus = $"{profile.Summary.Name} is active. The previous active profile pointer is kept for rollback.";
        AppendLog($"Set active local profile: {profile.Summary.Name}", "SUCCESS");
    }

    private async Task CreateLocalProfileAsync()
    {
        var profile = await _profileService.CreateFromConfigurationAsync(
            ProfileNameText,
            ProfileDescriptionText,
            BuildConfiguration("Custom"));
        await RefreshLocalProfilesAsync(profile.Summary.Id);
        ProfileOperationStatus = $"{profile.Summary.Name} was saved from the current Custom selections.";
        AppendLog($"Saved local profile: {profile.Summary.Name}", "SUCCESS");
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
        ProfileOperationStatus = $"{profile.Summary.Name} was duplicated from {sourceName}.";
        AppendLog($"Duplicated local profile: {profile.Summary.Name}", "SUCCESS");
    }

    private async Task RenameLocalProfileAsync()
    {
        if (SelectedLocalProfile is null)
        {
            return;
        }

        var profile = await _profileService.RenameAsync(SelectedLocalProfile.Id, ProfileNameText);
        await RefreshLocalProfilesAsync(profile.Summary.Id);
        ProfileOperationStatus = $"{profile.Summary.Name} was renamed.";
        AppendLog($"Renamed local profile: {profile.Summary.Name}", "SUCCESS");
    }

    private Task DeleteLocalProfileAsync()
    {
        if (SelectedLocalProfile is null)
        {
            return Task.CompletedTask;
        }

        var profile = SelectedLocalProfile;
        ShowPrompt(
            $"Delete profile: {profile.Name}",
            "LibreSpot will remove this local profile file. Bundled templates are kept, and deleting the active profile falls back to Recommended.",
            "Delete profile",
            "Keep profile",
            true,
            () => DeleteLocalProfileConfirmedAsync(profile.Id, profile.Name),
            "What this removes",
            "Only the selected local profile JSON file is deleted. config.json and bundled templates are left intact.");
        return Task.CompletedTask;
    }

    private async Task DeleteLocalProfileConfirmedAsync(string id, string name)
    {
        await _profileService.DeleteAsync(id);
        await RefreshLocalProfilesAsync();
        await RefreshSnapshotAsync();
        ProfileOperationStatus = $"{name} was deleted. Active fallback is visible in the profile list.";
        AppendLog($"Deleted local profile: {name}", "WARN");
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
            Title = "Export LibreSpot profile",
            Filter = "LibreSpot profiles (*.librespot)|*.librespot",
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
        ProfileOperationStatus = $"{SelectedLocalProfile.Name} was exported to {dialog.FileName}.";
        AppendLog($"Exported local profile: {dialog.FileName}", "SUCCESS");
    }

    private async Task RefreshSelectedProfileShareCardAsync()
    {
        var selected = SelectedLocalProfile;
        _selectedProfileShareCard = null;
        SelectedProfileQrImage = null;
        SelectedProfileShareStatus = selected is null
            ? "Select a profile to create an inert share card."
            : "Preparing share card...";
        SelectedProfileComparisonText = selected is null
            ? "Select a profile to compare it with Recommended."
            : "Comparing with Recommended...";
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
                SelectedProfileShareStatus = "QR card and share link are ready. Import still opens as a preview before saving.";
            }
            catch (Exception ex)
            {
                SelectedProfileQrImage = null;
                SelectedProfileShareStatus = $"Share link is ready, but this profile is too large for a QR card: {ex.Message}";
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
            SelectedProfileShareStatus = $"Couldn't prepare sharing for {selected.Name}: {ex.Message}";
            SelectedProfileComparisonText = "Comparison is unavailable until the profile can be loaded.";
            RaiseProfileShareCardStateChanged();
        }
    }

    private void RaiseProfileShareCardStateChanged()
    {
        OnPropertyChanged(nameof(HasSelectedProfileShareCard));
        OnPropertyChanged(nameof(SelectedProfileShareUri));
        OnPropertyChanged(nameof(HasSelectedProfileQrImage));
        CopyProfileShareUriCommand.NotifyCanExecuteChanged();
        CopyProfileComparisonCommand.NotifyCanExecuteChanged();
    }

    private void CopyProfileShareUri()
    {
        if (_selectedProfileShareCard is null)
        {
            return;
        }

        TryCopyText(_selectedProfileShareCard.ShareUri, "Copied profile share link.", "Clipboard was unavailable. Use Export to share the profile file instead.");
    }

    private void CopyProfileComparison()
    {
        if (SelectedLocalProfile is null)
        {
            return;
        }

        TryCopyText(SelectedProfileComparisonText, "Copied profile comparison.", "Clipboard was unavailable. The comparison remains visible here.");
    }

    private void TryCopyText(string text, string successMessage, string failureMessage)
    {
        try
        {
            Clipboard.SetText(text);
            ProfileOperationStatus = successMessage;
        }
        catch
        {
            ProfileOperationStatus = failureMessage;
        }
    }

    private static string BuildProfileComparison(InstallConfiguration configuration)
    {
        var normalized = AppCatalog.NormalizeConfiguration(configuration);
        var recommended = AppCatalog.CreateRecommendedConfiguration();
        var changedAreas = new List<string>();

        if (!string.Equals(normalized.Spicetify_Theme, recommended.Spicetify_Theme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(normalized.Spicetify_Scheme, recommended.Spicetify_Scheme, StringComparison.OrdinalIgnoreCase))
        {
            changedAreas.Add($"theme {normalized.Spicetify_Theme}/{normalized.Spicetify_Scheme}");
        }

        if (!string.Equals(normalized.SpotX_LyricsTheme, recommended.SpotX_LyricsTheme, StringComparison.OrdinalIgnoreCase))
        {
            changedAreas.Add($"lyrics color {Prettify.Label(normalized.SpotX_LyricsTheme)}");
        }

        if (!SetEquals(normalized.Spicetify_Extensions, recommended.Spicetify_Extensions))
        {
            changedAreas.Add($"{normalized.Spicetify_Extensions.Count} extensions");
        }

        if (normalized.Spicetify_CustomApps.Count > 0)
        {
            changedAreas.Add($"{normalized.Spicetify_CustomApps.Count} custom apps");
        }

        if (normalized.SpotX_Premium != recommended.SpotX_Premium)
        {
            changedAreas.Add("Premium account patch posture");
        }

        if (normalized.SpotX_CustomPatchesEnabled)
        {
            changedAreas.Add("custom SpotX patches");
        }

        if (normalized.CleanInstall != recommended.CleanInstall)
        {
            changedAreas.Add(normalized.CleanInstall ? "clean install" : "overlay install");
        }

        var diffText = changedAreas.Count == 0
            ? "matches the Recommended baseline"
            : $"differs in {string.Join(", ", changedAreas)}";
        return $"{normalized.Mode} profile {diffText}. Theme: {normalized.Spicetify_Theme}/{normalized.Spicetify_Scheme}; lyrics: {Prettify.Label(normalized.SpotX_LyricsTheme)}; extensions: {normalized.Spicetify_Extensions.Count}; custom apps: {normalized.Spicetify_CustomApps.Count}; custom patches: {(normalized.SpotX_CustomPatchesEnabled ? "on" : "off")}.";
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
            });
        }
        catch (Exception ex)
        {
            ProfileOperationStatus = $"Couldn't open link: {ex.Message}";
        }
    }

    private async Task ImportLocalProfileAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import LibreSpot profile",
            Filter = "LibreSpot profiles (*.librespot)|*.librespot|JSON files (*.json)|*.json",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var preview = await _profileService.PreviewImportAsync(dialog.FileName);
        ShowPrompt(
            $"Import profile: {preview.Name}",
            "LibreSpot validated the profile file and will save it as a local profile only after you confirm. Importing does not start an install or acknowledge Spotify risk.",
            "Import profile",
            Strings.ButtonCancel,
            false,
            () => ImportLocalProfileConfirmedAsync(preview),
            "Imported settings",
            BuildProfileSummary(preview.Configuration));
    }

    public async Task PreviewSharedProfileUriAsync(string shareUri)
    {
        var preview = await _profileService.PreviewShareUriAsync(shareUri);
        ShowPrompt(
            $"Import shared profile: {preview.Name}",
            "LibreSpot opened this share link as a preview only. Confirming saves it as a local profile; it does not start setup, reapply Spotify, or acknowledge Spotify risk.",
            "Save shared profile",
            Strings.ButtonCancel,
            false,
            () => ImportLocalProfileConfirmedAsync(preview),
            "Shared settings",
            BuildProfileSummary(preview.Configuration));
    }

    private async Task ImportLocalProfileConfirmedAsync(LocalProfileImportPreview preview)
    {
        var profile = await _profileService.ImportAsync(preview);
        await RefreshLocalProfilesAsync(profile.Summary.Id);
        ProfileOperationStatus = $"{profile.Summary.Name} was imported. Preview or set it active when you are ready.";
        AppendLog($"Imported local profile: {profile.Summary.Name}", "SUCCESS");
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

    private static string BuildProfileSummary(InstallConfiguration configuration)
    {
        var normalized = AppCatalog.NormalizeConfiguration(configuration);
        var extensionCount = normalized.Spicetify_Extensions.Count;
        var extensionText = extensionCount switch
        {
            0 => "no extensions",
            1 => "1 extension",
            _ => $"{extensionCount} extensions"
        };
        return $"{normalized.Mode} profile. Theme: {normalized.Spicetify_Theme} / {normalized.Spicetify_Scheme}. Lyrics: {Prettify.Label(normalized.SpotX_LyricsTheme)}. Extensions: {extensionText}. Premium flag: {(normalized.SpotX_Premium ? "on" : "off")}. Custom patches: {(normalized.SpotX_CustomPatchesEnabled ? "on" : "off")}.";
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
                "Clear cache",
                "Remove stale or corrupt cached downloads. Verified assets will be cached again on demand.",
                false,
                CreateAsyncCommand(
                    () => RunMaintenanceAsync(new MaintenanceActionDefinition(
                        "ClearCache",
                        "Clear asset cache",
                        "Remove stale or corrupt cached downloads. Verified assets will be cached again on demand.",
                        "Clear cache")),
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
            _customPatchValidation.IsValid ? "Custom patches dry run passed" : "Custom patches need review",
            _customPatchValidation.Summary,
            _customPatchValidation.IsValid
                ? "LibreSpot will stage a temporary patches.json when you apply this profile."
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
                "Custom patches formatted",
                "The patches.json document was normalized with indentation for review.",
                "Dry run still checks regex patterns and match/replace pairs before Apply.");
        }
        catch (JsonException ex)
        {
            ShowNotice(
                "Custom patches need review",
                $"JSON could not be formatted: {ex.Message}",
                "Fix the JSON syntax, then run Format again.");
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
                _customPatchValidation.IsValid ? "Custom patches imported" : "Imported patches need review",
                _customPatchValidation.Summary,
                _customPatchValidation.IsValid
                    ? $"Review the JSON before applying this profile. Source SHA256 {_customPatchesSourceSha256}."
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
            "Run recommended setup",
            planSummary,
            Strings.ButtonRunSetup,
            Strings.ButtonCancel,
            false,
            () => StartBackendRunAsync(
                "Install",
                configuration,
                "Applying the recommended setup",
                "LibreSpot is rebuilding the tested SpotX and Spicetify stack with the default premium preset.",
                0),
            "What this will do",
            "LibreSpot will download, verify, and apply each step listed above. The window stays open so you can follow progress.");
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
                "Custom patches need review",
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
            "Apply custom profile",
            planSummary,
            Strings.ButtonRunSetup,
            Strings.ButtonCancel,
            false,
            () => StartBackendRunAsync(
                "Install",
                configuration,
                "Applying your custom setup",
                "LibreSpot is validating your selections before it patches Spotify and restores the chosen visual stack.",
                1),
            "What this will do",
            "LibreSpot will download, verify, and apply each step listed above. The window stays open so you can follow progress.");
    }

    private async Task<string> CollectPlanSummaryAsync(InstallConfiguration configuration)
    {
        var planLines = new List<string>();
        // Plan is read-only, so the candidate configuration goes to a temp
        // file instead of config.json. The persistent save happens in
        // StartBackendRunAsync only after the user confirms the prompt —
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
            return "LibreSpot will download, verify, and apply the selected setup.";
        }

        var sb = new System.Text.StringBuilder();

        if (compatWarnings.Count > 0)
        {
            sb.AppendLine("⚠ Version compatibility warning:");
            foreach (var warning in compatWarnings)
            {
                sb.AppendLine(warning);
            }
            sb.AppendLine();
        }

        if (planLines.Count > 0)
        {
            sb.AppendLine("LibreSpot will perform these steps:");
            sb.AppendLine();
            foreach (var line in planLines)
            {
                sb.Append("• ");
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
            ? $"{definition.Description}{Environment.NewLine}{Environment.NewLine}Only LibreSpot's own data is removed — Spotify, SpotX patches, and Spicetify are not touched."
            : definition.IsDestructive
                ? $"{definition.Description}{Environment.NewLine}{Environment.NewLine}This is a deeper reset path and may remove the current customization state. Continue only when you are ready to rebuild."
                : $"{definition.Description}{Environment.NewLine}{Environment.NewLine}LibreSpot will keep this window open and stream backend progress while the action runs.";
        var (summaryTitle, summaryBody) = BuildMaintenancePromptSummary(definition);
        var requiresAdministrator = RequiresAdministrator(definition.Action);

        ShowPrompt(
            definition.Title,
            body,
            definition.ButtonText,
            definition.IsDestructive ? "Keep current setup" : Strings.ButtonCancel,
            definition.IsDestructive,
            () => StartBackendRunAsync(definition.Action, null, definition.Title, definition.Description, 2, requiresAdministrator),
            summaryTitle,
            summaryBody);
    }

    private static bool RequiresAdministrator(string action) =>
        action is not ("CheckUpdates" or "CreateBackup" or "OpenMarketplace" or "RemoveSelfData" or "ClearCache" or "Plan");

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
            Strings.ButtonCancel,
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

        SelectedWorkspaceIndex = targetWorkspaceIndex;
        ClearUndoActionItems();
        ClearLog();
        _activityOutcome = ActivityOutcome.None;
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
                    AppendLog("Configuration save was canceled.", "WARN");
                    _activityOutcome = ActivityOutcome.Canceled;
                    ActivityStatus = Strings.Canceled;
                    ActivityStep = Strings.ConfigSaveCanceled;
                    return;
                }
                catch (Exception ex)
                {
                    AppendLog($"Could not save configuration: {ex.Message}", "ERROR");
                    _activityOutcome = ActivityOutcome.Error;
                    ActivityStatus = Strings.RunNeedsAttention;
                    ActivityStep = "Configuration save failed";
                    ProgressValue = 100;
                    return;
                }

                ApplyConfigurationToEditor(configuration);
            }

            var result = await _backendScriptService.RunAsync(action, _configurationService.ConfigPath, HandleBackendMessage, token);
            if (result.Canceled)
            {
                AppendLog(result.ErrorMessage ?? "Backend run was canceled.", "WARN");
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
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog("Backend run was canceled.", "WARN");
            _activityOutcome = ActivityOutcome.Canceled;
            ActivityStatus = Strings.Canceled;
        }
        catch (Exception ex)
        {
            AppendLog($"Backend run failed: {ex.Message}", "ERROR");
            _activityOutcome = ActivityOutcome.Error;
            ActivityStatus = Strings.RunNeedsAttention;
        }
        finally
        {
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
                        ActivityStep = "LibreSpot is ready";
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
        try
        {
            _environmentState.Update(
                await _snapshotService.GetSnapshotAsync(_configurationService.ConfigPath),
                DateTime.Now);
            RaiseSnapshotInsightsChanged();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Environment snapshot refresh failed");
        }
    }

    private void RaiseSnapshotFreshnessChanged() => _environmentState.RefreshFreshness();

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
                ActivityStatus = Strings.StoppingBackend;
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

    private static (string Title, string Body) BuildMaintenancePromptSummary(MaintenanceActionDefinition definition) =>
        definition.Action switch
        {
            "CheckUpdates" => ("What this does", "LibreSpot compares pinned versions plus the SpotX, Spicetify CLI, Marketplace, and themes compatibility matrix before you decide whether to update."),
            "ClearCache" => ("What this does", "LibreSpot removes only its verified download cache and writes an operation receipt. Future installs rebuild cache entries after hash verification."),
            "Reapply" => ("What this does", "LibreSpot refreshes SpotX first, then restores the saved Spicetify layer so the stack returns to its last known profile."),
            "RepairMarketplace" => ("What this does", "LibreSpot reinstalls the Marketplace custom app, re-enables it in Spicetify, applies the change, and opens spotify:app:marketplace if Spotify accepts the URI."),
            "OpenMarketplace" => ("What this does", "LibreSpot asks Spotify to open spotify:app:marketplace without reinstalling or changing your Spicetify files."),
            "RestoreVanilla" => ("What this does", "This removes the visible Spicetify layer while leaving SpotX in place, so Spotify returns to a calmer default look."),
            "UninstallSpicetify" => ("What this removes", "LibreSpot restores Spotify first, then removes the Spicetify CLI, config folder, and PATH entry from this machine."),
            "FullReset" => ("What this removes", "LibreSpot clears Spotify customization state and related leftovers so the next install can start from a truly clean baseline."),
            "RemoveSelfData" => ("What this removes", "LibreSpot deletes its own configuration, profiles, backups, logs, crash reports, and the watcher task. Spotify, SpotX patches, and Spicetify are not touched."),
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
        _activityState.ShowNotice(title, status, step);
        OnPropertyChanged(nameof(RunElapsedText));
    }

    public void ApplyUiAutomationSmokeState(string state)
    {
        switch (state.Trim().ToLowerInvariant())
        {
            case "custom":
                SelectedWorkspaceIndex = 1;
                SettingsSearchText = "theme";
                break;
            case "maintenance":
                SelectedWorkspaceIndex = 2;
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
                        "Unregister the scheduled task to undo.")
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

        if (CustomPatchesEnabled)
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                _customPatchValidation.IsValid ? "accent" : "warning",
                _customPatchValidation.IsValid ? "Custom Patches Dry Run Ready" : "Custom Patches Need Review",
                CustomPatchesSummary));
        }

        if (selectedCustomApps.Length > 0)
        {
            SelectionInsights.Add(new SelectionInsightViewModel(
                "info",
                "Custom apps",
                $"{string.Join(", ", selectedCustomApps)} will be installed through Spicetify custom_apps."));
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
