namespace LibreSpot.Desktop.ViewModels;

public sealed class SettingsSearchStateViewModel : ObservableObject
{
    private string _text = string.Empty;

    public string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value))
            {
                RaisePropertyChanged(nameof(HasText));
                RaisePropertyChanged(nameof(Query));
            }
        }
    }

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
