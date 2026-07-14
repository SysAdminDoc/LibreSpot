namespace LibreSpot.Desktop.ViewModels;

public sealed class GlobalSearchResultViewModel
{
    private readonly string _keywords;

    public GlobalSearchResultViewModel(
        string automationId,
        int categoryOrder,
        string category,
        string title,
        string description,
        string keywords,
        string openHelpText,
        Action open)
    {
        AutomationId = automationId;
        CategoryOrder = categoryOrder;
        Category = category;
        Title = title;
        Description = description;
        _keywords = keywords;
        OpenHelpText = openHelpText;
        OpenCommand = new RelayCommand(open);
    }

    public string AutomationId { get; }
    public int CategoryOrder { get; }
    public string Category { get; }
    public string Title { get; }
    public string Description { get; }
    public string OpenHelpText { get; }
    public IRelayCommand OpenCommand { get; }
    public string AutomationName => $"{Category}: {Title}";

    public int MatchScore(string query)
    {
        var value = query.Trim();
        if (Title.Equals(value, StringComparison.CurrentCultureIgnoreCase))
        {
            return 0;
        }

        if (Title.StartsWith(value, StringComparison.CurrentCultureIgnoreCase))
        {
            return 1;
        }

        if (Title.Contains(value, StringComparison.CurrentCultureIgnoreCase))
        {
            return 2;
        }

        if (_keywords.Contains(value, StringComparison.CurrentCultureIgnoreCase))
        {
            return 3;
        }

        if (Description.Contains(value, StringComparison.CurrentCultureIgnoreCase))
        {
            return 4;
        }

        var searchableText = $"{Category} {Title} {Description} {_keywords}";
        var tokens = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Length > 1 && tokens.All(token =>
            searchableText.Contains(token, StringComparison.CurrentCultureIgnoreCase))
                ? 5
                : int.MaxValue;
    }
}
