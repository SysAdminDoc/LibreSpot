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
        RefreshGlobalSearch();
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
        ShowLocalProfileImportPrompt(preview);
    }

    public async Task PreviewLocalProfileFileAsync(string filePath)
    {
        try
        {
            var preview = await _profileService.PreviewImportAsync(filePath);
            ShowLocalProfileImportPrompt(preview);
        }
        catch (OperationCanceledException)
        {
            // Startup activation can be canceled during shutdown.
        }
        catch (Exception ex)
        {
            ProfileOperationStatus = ex.Message;
            HandleAsyncCommandException(ex);
        }
    }

    private void ShowLocalProfileImportPrompt(LocalProfileImportPreview preview)
    {
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
}

