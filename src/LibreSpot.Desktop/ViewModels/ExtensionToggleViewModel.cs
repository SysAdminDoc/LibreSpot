namespace LibreSpot.Desktop.ViewModels;

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
