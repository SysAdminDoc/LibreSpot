namespace LibreSpot.Desktop.ViewModels;

public sealed class ExtensionToggleViewModel : ObservableObject
{
    private bool _isSelected;
    private string _title;
    private string _description;
    private string _knownIssueNotice;

    public ExtensionToggleViewModel(
        string key,
        string title,
        string description,
        bool isRecommendedDefault,
        string knownIssueNotice = "")
    {
        Key = key;
        _title = title;
        _description = description;
        _knownIssueNotice = knownIssueNotice;
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

    // Empty unless the catalog records open upstream defects for this asset.
    // The Custom Install list has to say so before the box is ticked.
    public string KnownIssueNotice
    {
        get => _knownIssueNotice;
        private set
        {
            if (SetProperty(ref _knownIssueNotice, value))
            {
                OnPropertyChanged(nameof(HasKnownIssues));
            }
        }
    }

    public bool HasKnownIssues => !string.IsNullOrWhiteSpace(_knownIssueNotice);

    public void RefreshText(string title, string description, string knownIssueNotice = "")
    {
        Title = title;
        Description = description;
        KnownIssueNotice = knownIssueNotice;
    }
}
