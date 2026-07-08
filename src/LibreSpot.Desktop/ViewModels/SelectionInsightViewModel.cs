namespace LibreSpot.Desktop.ViewModels;

public sealed class SelectionInsightViewModel
{
    public SelectionInsightViewModel(string tone, string title, string detail)
    {
        Tone = tone;
        Title = title;
        Detail = detail;
    }

    public string Tone { get; }
    public string Title { get; }
    public string Detail { get; }
}
