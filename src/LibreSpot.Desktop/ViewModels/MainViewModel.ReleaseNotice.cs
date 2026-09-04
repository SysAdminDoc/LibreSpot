using System.Windows.Input;
using LibreSpot.Desktop.Services;

namespace LibreSpot.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    private readonly Func<string, CancellationToken, Task<ReleaseNotice>>? _releaseNoticeProbe;
    private readonly CancellationTokenSource _releaseNoticeCts = new();
    private ReleaseNotice? _libreSpotUpdateNotice;
    private Task? _libreSpotUpdateCheck;
    private bool _isLibreSpotUpdateVerificationExpanded;
    private bool? _libreSpotUpdateVerificationCopySucceeded;

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
    public bool HasLibreSpotUpdateVerification =>
        _libreSpotUpdateNotice is { UpdateAvailable: true, DesktopAssetDigest: not null };
    public string LibreSpotUpdateDigest => HasLibreSpotUpdateVerification
        ? _libreSpotUpdateNotice!.DesktopAssetDigest!
        : string.Empty;
    public string LibreSpotUpdateVerificationCommandText =>
        HasLibreSpotUpdateVerification && _libreSpotUpdateNotice is { LatestVersion: not null }
            ? $"gh release verify-asset -R SysAdminDoc/LibreSpot v{_libreSpotUpdateNotice.LatestVersion} .\\{ReleaseNoticeService.DesktopAssetName}"
            : string.Empty;
    public string LibreSpotUpdateVerificationStatus => _libreSpotUpdateVerificationCopySucceeded switch
    {
        true => L("Vm_LibreSpotUpdateVerifyCopied"),
        false => L("Vm_LibreSpotUpdateVerifyClipboardUnavailable"),
        null => string.Empty
    };
    public bool IsLibreSpotUpdateVerificationExpanded
    {
        get => _isLibreSpotUpdateVerificationExpanded;
        set => SetProperty(ref _isLibreSpotUpdateVerificationExpanded, value);
    }
    public ICommand OpenLibreSpotUpdateCommand { get; }
    public ICommand CopyLibreSpotUpdateVerificationCommand { get; }
    public Task LibreSpotUpdateCheck => _libreSpotUpdateCheck ?? Task.CompletedTask;

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
            _libreSpotUpdateVerificationCopySucceeded = null;
            if (!HasLibreSpotUpdateVerification)
            {
                IsLibreSpotUpdateVerificationExpanded = false;
            }
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
        OnPropertyChanged(nameof(HasLibreSpotUpdateVerification));
        OnPropertyChanged(nameof(LibreSpotUpdateDigest));
        OnPropertyChanged(nameof(LibreSpotUpdateVerificationCommandText));
        OnPropertyChanged(nameof(LibreSpotUpdateVerificationStatus));
        (OpenLibreSpotUpdateCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (CopyLibreSpotUpdateVerificationCommand as RelayCommand)?.NotifyCanExecuteChanged();
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

    private void CopyLibreSpotUpdateVerification()
    {
        if (!HasLibreSpotUpdateVerification)
        {
            return;
        }

        _libreSpotUpdateVerificationCopySucceeded = TrySetClipboardText(LibreSpotUpdateVerificationCommandText);
        OnPropertyChanged(nameof(LibreSpotUpdateVerificationStatus));
        AppendLog(
            LibreSpotUpdateVerificationStatus,
            _libreSpotUpdateVerificationCopySucceeded == true ? "INFO" : "WARN");
    }
}
