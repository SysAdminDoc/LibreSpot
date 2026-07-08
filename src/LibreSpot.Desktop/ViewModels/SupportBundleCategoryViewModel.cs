using LibreSpot.Desktop.Properties;
using LibreSpot.Desktop.Services;

namespace LibreSpot.Desktop.ViewModels;

public sealed class SupportBundleCategoryViewModel : ObservableObject
{
    private readonly Action _selectionChanged;
    private bool _isRefreshing;
    private bool _isSelected;
    private string _detail;
    private string _fileCountText = Strings.FilesNone;
    private string _estimatedSizeText = Strings.SizeNone;

    public SupportBundleCategoryViewModel(
        string id,
        string title,
        bool isRequired,
        bool isSelected,
        string detail,
        Action selectionChanged)
    {
        Id = id;
        Title = title;
        IsRequired = isRequired;
        _isSelected = isRequired || isSelected;
        _detail = detail;
        _selectionChanged = selectionChanged;
    }

    public string Id { get; }
    public string Title { get; }
    public bool IsRequired { get; }
    public bool IsOptional => !IsRequired;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            var next = IsRequired || value;
            if (SetProperty(ref _isSelected, next) && !_isRefreshing)
            {
                _selectionChanged();
            }
        }
    }

    public string Detail
    {
        get => _detail;
        private set => SetProperty(ref _detail, value);
    }

    public string FileCountText
    {
        get => _fileCountText;
        private set => SetProperty(ref _fileCountText, value);
    }

    public string EstimatedSizeText
    {
        get => _estimatedSizeText;
        private set => SetProperty(ref _estimatedSizeText, value);
    }

    public void Refresh(SupportBundlePreviewEntry entry)
    {
        _isRefreshing = true;
        try
        {
            Detail = entry.Detail;
            FileCountText = entry.FileCount == 1
                ? ViewModelText.Get("Vm_FileCountOne")
                : ViewModelText.Format("Vm_FileCountManyFormat", entry.FileCount);
            EstimatedSizeText = MainViewModel.FormatBytes(entry.EstimatedBytes);
            IsSelected = entry.IsSelected;
        }
        finally
        {
            _isRefreshing = false;
        }
    }
}
