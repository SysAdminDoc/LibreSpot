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

    private void RefreshGlobalSearch()
    {
        GlobalSearchResults.Clear();
        if (!HasGlobalSearchText)
        {
            RaiseGlobalSearchChanged();
            return;
        }

        var query = GlobalSearchText.Trim();
        var candidates = new List<GlobalSearchResultViewModel>();

        void Add(
            int categoryOrder,
            string categoryKey,
            string title,
            string description,
            string keywords,
            Action open)
        {
            var category = L(categoryKey);
            var id = $"GlobalSearchResult_{categoryOrder}_{candidates.Count}";
            candidates.Add(new GlobalSearchResultViewModel(
                id,
                categoryOrder,
                category,
                title,
                description,
                keywords,
                LF("Vm_GlobalSearchOpenResultHelpFormat", title, category),
                () =>
                {
                    open();
                    GlobalSearchText = string.Empty;
                }));
        }

        Add(0, "Vm_GlobalSearchCategorySetup", L("ModeRecommendedTitle"), L("ModeRecommendedDescription"),
            $"recommended install apply {L("ButtonRunRecommendedSetup")}", () => OpenGlobalWorkspace(0));
        Add(0, "Vm_GlobalSearchCategorySetup", L("ModeCustomTitle"), L("Vm_WorkspaceHeroCustomBody"),
            "custom configure settings profile", () => OpenGlobalWorkspace(1));
        Add(0, "Vm_GlobalSearchCategorySetup", L("ModeMaintenanceTitle"), L("Vm_WorkspaceHeroMaintenanceBody"),
            "maintenance repair diagnostics support", () => OpenGlobalWorkspace(2));

        foreach (var option in EnumerateAllOptions())
        {
            Add(1, "Vm_GlobalSearchCategorySettings", option.Title, option.Description, option.Key,
                () => OpenGlobalCustomFilter(option.Title));
        }

        Add(1, "Vm_GlobalSearchCategorySettings", L("Vm_SearchColorSchemeTitle"), ThemeSchemeHint, "color palette appearance",
            () => OpenGlobalCustomFilter(L("Vm_SearchColorSchemeTitle")));
        Add(1, "Vm_GlobalSearchCategorySettings", L("Vm_SearchLyricsThemeTitle"), LyricsThemeHint, "lyrics colors appearance",
            () => OpenGlobalCustomFilter(L("Vm_SearchLyricsThemeTitle")));
        Add(1, "Vm_GlobalSearchCategorySettings", L("Vm_SearchCacheLimitTitle"), L("Vm_SearchCacheLimitDescription"), "cache storage",
            () => OpenGlobalCustomFilter(L("Vm_SearchCacheLimitTitle")));
        Add(1, "Vm_GlobalSearchCategorySettings", L("Vm_SearchSpotifyBuildTitle"), SpotifyVersionNotes, "spotify version architecture",
            () => OpenGlobalCustomFilter(L("Vm_SearchSpotifyBuildTitle")));
        Add(1, "Vm_GlobalSearchCategorySettings", L("Vm_SearchDownloadPathTitle"), DownloadMethodDetail, "download method network",
            () => OpenGlobalCustomFilter(L("Vm_SearchDownloadPathTitle")));
        Add(1, "Vm_GlobalSearchCategorySettings", L("Vm_CustomPatchesSearchTitle"), L("Vm_CustomPatchesSearchDescription"), "spotx json patches advanced",
            () => OpenGlobalCustomFilter(L("Vm_CustomPatchesSearchTitle")));

        foreach (var theme in ThemeGalleryItems)
        {
            Add(2, "Vm_GlobalSearchCategoryThemesAssets", theme.Label, theme.AutomationHelpText,
                $"theme {theme.Name} {theme.SourceBadge} {theme.JsBadge} {theme.SchemePreview}",
                () => OpenGlobalTheme(theme));
        }

        foreach (var extension in Extensions.Concat(CustomApps))
        {
            Add(2, "Vm_GlobalSearchCategoryThemesAssets", extension.Title, extension.Description,
                $"extension custom app asset {extension.Key}", () => OpenGlobalCustomFilter(extension.Title));
        }

        foreach (var profile in LocalProfiles)
        {
            Add(3, "Vm_GlobalSearchCategoryProfiles", profile.Name,
                profile.HasDescription ? profile.Description : profile.CapabilityText,
                $"profile preset {profile.Id} {profile.KindBadge} {profile.StateBadge}",
                () => OpenGlobalProfile(profile));
        }

        foreach (var action in SafeMaintenanceActions.Concat(DestructiveMaintenanceActions))
        {
            Add(4, "Vm_GlobalSearchCategoryMaintenance", action.Title, action.Description,
                $"{action.Action} {action.ButtonText} repair action", () => OpenGlobalWorkspace(2));
        }

        Add(5, "Vm_GlobalSearchCategorySupport", L("SupportBundleTitle"), SupportBundleRedactionSummary,
            $"{L("ButtonExportBundle")} diagnostics logs crash journal", () => OpenGlobalWorkspace(2));
        foreach (var item in SupportBundleItems)
        {
            Add(5, "Vm_GlobalSearchCategorySupport", item.Title, item.Detail,
                $"support bundle {item.Id}", () => OpenGlobalWorkspace(2));
        }

        foreach (var issue in HealthReport.CriticalIssues
                     .Concat(HealthReport.WarningIssues)
                     .Concat(HealthReport.InfoIssues))
        {
            Add(6, "Vm_GlobalSearchCategoryHealthTrust", issue.Name,
                $"{issue.Status}. {issue.Evidence}",
                $"health trust {issue.Id} {issue.RecommendedActionText}", () => OpenGlobalWorkspace(2));
        }

        foreach (var provenance in ShellProvenanceItems)
        {
            Add(6, "Vm_GlobalSearchCategoryHealthTrust", provenance.Name,
                $"{provenance.PinnedDetail}. {provenance.VerifiedDetail}",
                $"trust source provenance {provenance.FreshnessText} {provenance.SourceUrl}", () => OpenGlobalWorkspace(0));
        }

        foreach (var result in candidates
                     .Select(candidate => (Candidate: candidate, Score: candidate.MatchScore(query)))
                     .Where(match => match.Score != int.MaxValue)
                     .OrderBy(match => match.Score)
                     .ThenBy(match => match.Candidate.CategoryOrder)
                     .ThenBy(match => match.Candidate.Title, StringComparer.CurrentCultureIgnoreCase)
                     .Take(48)
                     .Select(match => match.Candidate))
        {
            GlobalSearchResults.Add(result);
        }

        RaiseGlobalSearchChanged();
    }

    private void RaiseGlobalSearchChanged()
    {
        OnPropertyChanged(nameof(HasGlobalSearchText));
        OnPropertyChanged(nameof(HasGlobalSearchResults));
        OnPropertyChanged(nameof(ShowGlobalSearchEmptyState));
        OnPropertyChanged(nameof(GlobalSearchSummary));
        ClearGlobalSearchCommand.NotifyCanExecuteChanged();
    }

    private void OpenGlobalWorkspace(int workspaceIndex)
    {
        SelectedWorkspaceIndex = workspaceIndex;
        if (workspaceIndex == 1)
        {
            SettingsSearchText = string.Empty;
            ThemeSearchText = string.Empty;
        }
    }

    private void OpenGlobalCustomFilter(string filter)
    {
        SelectedWorkspaceIndex = 1;
        ThemeSearchText = string.Empty;
        SettingsSearchText = filter;
    }

    private void OpenGlobalTheme(ThemeGalleryItemViewModel theme)
    {
        SelectedWorkspaceIndex = 1;
        SettingsSearchText = L("Vm_SearchThemePackTitle");
        ThemeSearchText = theme.Label;
        SelectedThemeGalleryItem = theme;
    }

    private void OpenGlobalProfile(LocalProfileCardViewModel profile)
    {
        SelectedWorkspaceIndex = 1;
        SettingsSearchText = string.Empty;
        ThemeSearchText = string.Empty;
        SelectedLocalProfile = profile;
    }

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
        OnPropertyChanged(nameof(HasSelectedExtensions));
        OnPropertyChanged(nameof(AccessPostureLabel));
        OnPropertyChanged(nameof(CustomChangeCountLabel));
        OnPropertyChanged(nameof(CustomProfileTitle));
        OnPropertyChanged(nameof(CustomProfileDetail));
        OnPropertyChanged(nameof(CustomRunReadinessTitle));
        OnPropertyChanged(nameof(CustomRunReadinessDetail));
        OnPropertyChanged(nameof(CustomApplyCaption));
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
}
