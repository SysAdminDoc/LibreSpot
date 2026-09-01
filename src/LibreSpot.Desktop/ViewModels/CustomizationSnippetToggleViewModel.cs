using LibreSpot.Desktop.Models;

namespace LibreSpot.Desktop.ViewModels;

public sealed class CustomizationSnippetToggleViewModel : ObservableObject
{
    private bool _isSelected;

    public CustomizationSnippetToggleViewModel(CustomizationSnippetDefinition definition)
    {
        Definition = definition;
    }

    public CustomizationSnippetDefinition Definition { get; }
    public string Id => Definition.Id;
    public string Title => Definition.Title;
    public string Description => Definition.Description;
    public string Category => Definition.Category;
    public string SourceTitle => Definition.SourceTitle;
    public string Preview => Definition.Preview;
    public string LastVerifiedSpotify => Definition.LastVerifiedSpotify;
    public bool IsLive => Definition.Live;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool Matches(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return Id.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               Description.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               Category.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }
}

public sealed record CustomizationGroupOption(string Key, string Label);
