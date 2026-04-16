using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;
using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;

namespace LibreSpot.Desktop.ViewModels;

public sealed class OptionToggleViewModel : ObservableObject
{
    private bool _isSelected;

    public OptionToggleViewModel(string key, string title, string description)
    {
        Key = key;
        Title = title;
        Description = description;
    }

    public string Key { get; }
    public string Title { get; }
    public string Description { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class ExtensionToggleViewModel : ObservableObject
{
    private bool _isSelected;

    public ExtensionToggleViewModel(string key, string title, string description)
    {
        Key = key;
        Title = title;
        Description = description;
    }

    public string Key { get; }
    public string Title { get; }
    public string Description { get; }

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

public sealed class MaintenanceActionCardViewModel
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string ButtonText { get; init; }
    public required bool IsDestructive { get; init; }
    public required RelayCommand Command { get; init; }
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
    private readonly Dispatcher _dispatcher;
    private readonly bool _isAdministratorSession;
    private CancellationTokenSource? _runCts;

    private string _selectedTheme = "(None - Marketplace Only)";
    private string _selectedScheme = "Default";
    private string _selectedLyricsTheme = "spotify";
    private string _cacheLimitText = "0";
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
    private bool _isPromptDestructive;
    private Func<Task>? _pendingPromptAction;

    public MainViewModel(
        ConfigurationService configurationService,
        BackendScriptService backendScriptService,
        EnvironmentSnapshotService snapshotService)
    {
        _configurationService = configurationService;
        _backendScriptService = backendScriptService;
        _snapshotService = snapshotService;
        _dispatcher = Application.Current.Dispatcher;
        _isAdministratorSession = IsAdministrator();

        RecommendedHighlights = new ObservableCollection<string>(AppCatalog.RecommendedHighlights);
        ThemeNames = new ObservableCollection<string>(AppCatalog.ThemeSchemes.Keys);
        SchemeOptions = new ObservableCollection<string>(AppCatalog.ThemeSchemes[_selectedTheme]);
        LyricsThemes = new ObservableCollection<string>(AppCatalog.LyricsThemes);

        InstallOptions = CreateOptions("Install");
        CoreOptions = CreateOptions("Core");
        InterfaceOptions = CreateOptions("Interface");
        AdvancedOptions = CreateOptions("Advanced");
        ExperienceOptions = CreateOptions("Experience");
        Extensions = new ObservableCollection<ExtensionToggleViewModel>(
            AppCatalog.ExtensionDefinitions.Select(def => new ExtensionToggleViewModel(def.Key, def.Title, def.Description)));

        var maintenanceCards = AppCatalog.MaintenanceActions
            .Select(def => new MaintenanceActionCardViewModel
            {
                Title = def.Title,
                Description = def.Description,
                ButtonText = def.ButtonText,
                IsDestructive = def.IsDestructive,
                Command = new RelayCommand(() => _ = RunMaintenanceAsync(def))
            })
            .ToList();

        SafeMaintenanceActions = new ObservableCollection<MaintenanceActionCardViewModel>(maintenanceCards.Where(card => !card.IsDestructive));
        DestructiveMaintenanceActions = new ObservableCollection<MaintenanceActionCardViewModel>(maintenanceCards.Where(card => card.IsDestructive));

        LogEntries = new ObservableCollection<LogEntryViewModel>();
        ApplyRecommendedCommand = new RelayCommand(() => _ = ApplyRecommendedAsync(), () => !IsRunning);
        ApplyCustomCommand = new RelayCommand(() => _ = ApplyCustomAsync(), () => !IsRunning);
        DismissActivityCommand = new RelayCommand(DismissActivity, () => IsActivityVisible && !IsRunning);
        CopyLogCommand = new RelayCommand(CopyLog, () => LogEntries.Count > 0);
        RefreshSnapshotCommand = new RelayCommand(RefreshSnapshot);
        RelaunchAsAdministratorCommand = new RelayCommand(PresentAdministratorPrompt, () => NeedsAdministratorRelaunch && !IsRunning);
        ConfirmPromptCommand = new RelayCommand(() => _ = ConfirmPromptAsync(), () => IsPromptVisible && !IsRunning);
        CancelPromptCommand = new RelayCommand(CancelPrompt, () => IsPromptVisible && !IsRunning);
        EscapeCommand = new RelayCommand(HandleEscape);

        RegisterOptionStateObservers();
        RaiseSelectionInsightsChanged();
        RaiseSnapshotInsightsChanged();
    }

    public ObservableCollection<string> RecommendedHighlights { get; }
    public ObservableCollection<string> ThemeNames { get; }
    public ObservableCollection<string> SchemeOptions { get; }
    public ObservableCollection<string> LyricsThemes { get; }

    public ObservableCollection<OptionToggleViewModel> InstallOptions { get; }
    public ObservableCollection<OptionToggleViewModel> CoreOptions { get; }
    public ObservableCollection<OptionToggleViewModel> InterfaceOptions { get; }
    public ObservableCollection<OptionToggleViewModel> AdvancedOptions { get; }
    public ObservableCollection<OptionToggleViewModel> ExperienceOptions { get; }
    public ObservableCollection<ExtensionToggleViewModel> Extensions { get; }
    public ObservableCollection<MaintenanceActionCardViewModel> SafeMaintenanceActions { get; }
    public ObservableCollection<MaintenanceActionCardViewModel> DestructiveMaintenanceActions { get; }
    public ObservableCollection<LogEntryViewModel> LogEntries { get; }

    public RelayCommand ApplyRecommendedCommand { get; }
    public RelayCommand ApplyCustomCommand { get; }
    public RelayCommand DismissActivityCommand { get; }
    public RelayCommand CopyLogCommand { get; }
    public RelayCommand RefreshSnapshotCommand { get; }
    public RelayCommand RelaunchAsAdministratorCommand { get; }
    public RelayCommand ConfirmPromptCommand { get; }
    public RelayCommand CancelPromptCommand { get; }
    public RelayCommand EscapeCommand { get; }

    public EnvironmentSnapshot Snapshot
    {
        get => _snapshot;
        private set => SetProperty(ref _snapshot, value);
    }

    public bool IsAdministratorSession => _isAdministratorSession;
    public bool NeedsAdministratorRelaunch => !_isAdministratorSession;

    public string SessionAccessTitle =>
        IsAdministratorSession ? "Administrator session" : "Standard session";

    public string SessionAccessDetail =>
        IsAdministratorSession
            ? "Install, reapply, and recovery can run without another prompt."
            : "You can browse settings here, but any action that modifies Spotify needs elevation.";

    public string SpotifyStatusLine =>
        Snapshot.SpotifyInstalled
            ? "Spotify detected"
            : "Spotify not installed";

    public string CustomizationStatusLine =>
        Snapshot.SpicetifyInstalled
            ? "Spicetify detected"
            : "Spicetify not installed";

    public string ProfileStatusLine =>
        Snapshot.SavedConfigExists
            ? "Saved LibreSpot profile found"
            : "No saved profile yet";

    public string WorkspaceRecommendationTitle =>
        Snapshot.SpotifyInstalled && Snapshot.SpicetifyInstalled
            ? "Refine or recover"
            : Snapshot.SpotifyInstalled
                ? "Finish the customization layer"
                : "Start with the recommended setup";

    public string WorkspaceRecommendationDetail =>
        Snapshot.SpotifyInstalled && Snapshot.SpicetifyInstalled
            ? "Recommended is still the cleanest way to stabilize the stack. Switch to Custom once you know the tweaks you want to keep."
            : Snapshot.SpotifyInstalled
                ? "Spotify is already installed — Recommended is the fastest way to restore a balanced setup."
                : "No Spotify customization stack yet. Recommended is the safest starting point.";

    public string CustomSelectionSummary
    {
        get
        {
            var enabledCount = EnumerateAllOptions().Count(option => option.IsSelected);
            return enabledCount switch
            {
                0 => "No toggles enabled. LibreSpot will fall back to the calmest baseline.",
                1 => "1 toggle enabled beyond the base preset.",
                _ => $"{enabledCount} toggles enabled across install, behavior, and interface."
            };
        }
    }

    public string ThemeSummary =>
        SelectedTheme == "(None - Marketplace Only)"
            ? "Marketplace-only (no theme pack)"
            : $"{SelectedTheme} · {Prettify.Label(SelectedScheme)}";

    public string LyricsSummary => $"Lyrics: {Prettify.Label(SelectedLyricsTheme)}";

    public string CacheSummary =>
        int.TryParse(CacheLimitText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? $"Cache ceiling: {parsed} MB"
            : "Cache: default";

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
            ? "Safe to refresh, roll back, or reset"
            : Snapshot.SpotifyInstalled
                ? "Customization layer is incomplete"
                : "Recovery tools are ready when you need them";

    public string MaintenanceGuidanceDetail =>
        Snapshot.SpotifyInstalled && Snapshot.SpicetifyInstalled
            ? "Use the left column for low-risk upkeep. Use the reset lane only when you want to clear the stack and rebuild."
            : Snapshot.SpotifyInstalled
                ? "Reapply becomes useful once the customization layer is back in place. Until then, Recommended is usually the better path."
                : "You can still inspect versions or prepare a clean reset. Most maintenance actions unlock once Spotify is installed.";

    public int SelectedWorkspaceIndex
    {
        get => _selectedWorkspaceIndex;
        set => SetProperty(ref _selectedWorkspaceIndex, value);
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
                DismissActivityCommand.RaiseCanExecuteChanged();
                RelaunchAsAdministratorCommand.RaiseCanExecuteChanged();
                ConfirmPromptCommand.RaiseCanExecuteChanged();
                CancelPromptCommand.RaiseCanExecuteChanged();
                RaisePropertyChanged(nameof(IsBusyIndeterminate));
                RaisePropertyChanged(nameof(ProgressLabel));
                RaisePropertyChanged(nameof(IsActivityError));
                RaisePropertyChanged(nameof(ActivityBadgeText));
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
                RaisePropertyChanged(nameof(ActivityBadgeText));
            }
        }
    }

    public bool IsBusyIndeterminate => _isRunning && _progressValue <= 0.0;

    // "— %" reads like a broken UI. When we don't yet have a real percentage
    // from the backend, say what is actually happening: we're working.
    public string ProgressLabel =>
        IsBusyIndeterminate
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
         _activityStatus.Contains("canceled", StringComparison.OrdinalIgnoreCase) ||
         _activityStatus.Contains("failed", StringComparison.OrdinalIgnoreCase));

    public string ActivityBadgeText =>
        _isRunning ? "Live run"
        : IsActivityError ? "Needs attention"
        : _progressValue >= 100 ? "Run complete"
        : "Ready";

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
                RaisePropertyChanged(nameof(ActivityBadgeText));
            }
        }
    }

    public string ActivityStep
    {
        get => _activityStep;
        private set => SetProperty(ref _activityStep, value);
    }

    public string LogLineCountText =>
        LogEntries.Count switch
        {
            0 => "Waiting for output",
            1 => "1 line",
            _ => $"{LogEntries.Count} lines"
        };

    public bool IsLogEmpty => LogEntries.Count == 0;

    public string LastRefreshedText =>
        _snapshotRefreshedAt is null
            ? string.Empty
            : $"Last refreshed {_snapshotRefreshedAt.Value.ToString("h:mm:ss tt", System.Globalization.CultureInfo.InvariantCulture)}";

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

    public bool IsPromptDestructive
    {
        get => _isPromptDestructive;
        private set => SetProperty(ref _isPromptDestructive, value);
    }

    public async Task InitializeAsync()
    {
        var config = await _configurationService.LoadAsync();
        ApplyConfigurationToEditor(config);
        RefreshSnapshot();
    }

    private ObservableCollection<OptionToggleViewModel> CreateOptions(string section) =>
        new(AppCatalog.OptionDefinitions
            .Where(def => def.Section == section)
            .Select(def => new OptionToggleViewModel(def.Key, def.Title, def.Description)));

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
            RaiseSelectionInsightsChanged();
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
        RaisePropertyChanged(nameof(CustomSelectionSummary));
        RaisePropertyChanged(nameof(ThemeSummary));
        RaisePropertyChanged(nameof(LyricsSummary));
        RaisePropertyChanged(nameof(CacheSummary));
        RaisePropertyChanged(nameof(ExtensionSummary));
    }

    private void RaiseSnapshotInsightsChanged()
    {
        RaisePropertyChanged(nameof(SessionAccessTitle));
        RaisePropertyChanged(nameof(SessionAccessDetail));
        RaisePropertyChanged(nameof(SpotifyStatusLine));
        RaisePropertyChanged(nameof(CustomizationStatusLine));
        RaisePropertyChanged(nameof(ProfileStatusLine));
        RaisePropertyChanged(nameof(WorkspaceRecommendationTitle));
        RaisePropertyChanged(nameof(WorkspaceRecommendationDetail));
        RaisePropertyChanged(nameof(MaintenanceGuidanceTitle));
        RaisePropertyChanged(nameof(MaintenanceGuidanceDetail));
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

        ShowPrompt(
            definition.Title,
            body,
            definition.ButtonText,
            definition.IsDestructive ? "Keep current setup" : "Cancel",
            definition.IsDestructive,
            () => StartBackendRunAsync(definition.Action, null, definition.Title, definition.Description, 2));

        return Task.CompletedTask;
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

        if (!IsAdministratorSession)
        {
            PresentAdministratorPrompt();
            return;
        }

        SelectedWorkspaceIndex = targetWorkspaceIndex;
        ActivityTitle = title;
        ActivityStatus = status;
        ActivityStep = "Preparing backend runtime";
        ClearLog();
        ProgressValue = 0;
        IsActivityVisible = true;
        IsRunning = true;

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
                }
                catch (OperationCanceledException)
                {
                    AppendLog("Configuration save was canceled.", "WARN");
                    return;
                }
                catch (Exception ex)
                {
                    AppendLog($"Could not save configuration: {ex.Message}", "WARN");
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
            IsRunning = false;
            RefreshSnapshot();
        }
    }

    /// <summary>
    /// Requests cancellation of an in-flight backend run. Safe to call during window
    /// shutdown — if no run is active this is a no-op.
    /// </summary>
    public void CancelRunningBackend() => _runCts?.Cancel();

    public void Dispose()
    {
        try { _runCts?.Cancel(); } catch { }
        _runCts?.Dispose();
        _runCts = null;
    }

    private void HandleBackendMessage(BackendMessage message)
    {
        _dispatcher.Invoke(() =>
        {
            switch (message.Kind)
            {
                case "progress":
                    if (double.TryParse(message.Payload, out var value))
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

    private void RefreshSnapshot()
    {
        Snapshot = _snapshotService.GetSnapshot(_configurationService.ConfigPath);
        _snapshotRefreshedAt = DateTime.Now;
        RaisePropertyChanged(nameof(LastRefreshedText));
        RaiseSnapshotInsightsChanged();
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
        // Try twice with a tiny delay before giving up so transient contention
        // (Office, clipboard managers, RDP) doesn't surface as a crash.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                if (attempt == 0)
                {
                    System.Threading.Thread.Sleep(60);
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
            });
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

            Process.Start(startInfo);
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
        Func<Task> confirmAction)
    {
        PromptTitle = title;
        PromptBody = body;
        PromptConfirmText = confirmText;
        PromptCancelText = cancelText;
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
        if (IsPromptVisible && !IsRunning)
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
        IsPromptDestructive = false;
        _pendingPromptAction = null;
    }

    private void ShowNotice(string title, string status, string step)
    {
        ActivityTitle = title;
        ActivityStatus = status;
        ActivityStep = step;
        ProgressValue = 0;
        IsActivityVisible = true;
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
        foreach (var scheme in AppCatalog.ThemeSchemes[SelectedTheme])
        {
            SchemeOptions.Add(scheme);
        }

        if (!SchemeOptions.Contains(SelectedScheme))
        {
            SelectedScheme = SchemeOptions.FirstOrDefault() ?? "Default";
        }
    }

    private void ApplyConfigurationToEditor(InstallConfiguration configuration)
    {
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
        CacheLimitText = configuration.SpotX_CacheLimit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SelectedScheme = AppCatalog.ThemeSchemes[SelectedTheme].Contains(configuration.Spicetify_Scheme)
            ? configuration.Spicetify_Scheme
            : AppCatalog.ThemeSchemes[SelectedTheme].First();

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

        return configuration;
    }

    private static void ApplyOptionsToConfiguration(IEnumerable<OptionToggleViewModel> options, InstallConfiguration configuration)
    {
        foreach (var option in options)
        {
            var property = typeof(InstallConfiguration).GetProperty(option.Key, BindingFlags.Public | BindingFlags.Instance);
            property?.SetValue(configuration, option.IsSelected);
        }
    }
}
