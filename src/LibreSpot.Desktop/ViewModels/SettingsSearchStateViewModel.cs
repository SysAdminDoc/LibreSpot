namespace LibreSpot.Desktop.ViewModels;

public sealed partial class SettingsSearchStateViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasText))]
    [NotifyPropertyChangedFor(nameof(Query))]
    private string _text = string.Empty;

    public bool HasText => !string.IsNullOrWhiteSpace(Text);

    public string Query => Text.Trim();

    public bool Matches(string title, string description)
    {
        if (!HasText)
        {
            return true;
        }

        return title.Contains(Query, StringComparison.OrdinalIgnoreCase) ||
               description.Contains(Query, StringComparison.OrdinalIgnoreCase);
    }
}
