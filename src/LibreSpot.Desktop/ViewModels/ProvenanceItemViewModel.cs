namespace LibreSpot.Desktop.ViewModels;

public sealed class ProvenanceItemViewModel
{
    public ProvenanceItemViewModel(
        string name,
        string pinnedDetail,
        string sourceUrl,
        string? releaseNotesUrl,
        string verifiedDetail,
        string freshnessText,
        string tone,
        string openSourceText,
        string openReleaseNotesText,
        Action<string> openExternalUri)
    {
        Name = name;
        PinnedDetail = pinnedDetail;
        SourceUrl = sourceUrl;
        ReleaseNotesUrl = releaseNotesUrl;
        VerifiedDetail = verifiedDetail;
        FreshnessText = freshnessText;
        Tone = tone;
        OpenSourceText = openSourceText;
        OpenReleaseNotesText = openReleaseNotesText;
        OpenSourceCommand = new RelayCommand(
            () => openExternalUri(SourceUrl),
            () => !string.IsNullOrWhiteSpace(SourceUrl));
        OpenReleaseNotesCommand = new RelayCommand(
            () => openExternalUri(ReleaseNotesUrl!),
            () => HasReleaseNotes);
    }

    public string Name { get; }
    public string PinnedDetail { get; }
    public string SourceUrl { get; }
    public string? ReleaseNotesUrl { get; }
    public string VerifiedDetail { get; }
    public string FreshnessText { get; }
    public string Tone { get; }
    public string OpenSourceText { get; }
    public string OpenReleaseNotesText { get; }
    public bool HasReleaseNotes => !string.IsNullOrWhiteSpace(ReleaseNotesUrl);
    public IRelayCommand OpenSourceCommand { get; }
    public IRelayCommand OpenReleaseNotesCommand { get; }
}
