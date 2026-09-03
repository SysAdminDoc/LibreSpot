using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using UserControl = System.Windows.Controls.UserControl;
using LibreSpot.Desktop.ViewModels;

namespace LibreSpot.Desktop.Views;

public partial class CustomWorkspaceView : UserControl
{
    private MainViewModel? _viewModel;

    public CustomWorkspaceView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public FrameworkElement ProfileSurface => ProfileSummarySection.ProfileSurface;

    public void SyncCustomPatchesEditorText(MainViewModel viewModel) =>
        CustomPatchesSection.SyncCustomPatchesEditorText(viewModel);

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = e.NewValue as MainViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    // A search that reveals a hidden option opens its group; the group must
    // also be on screen, so the first opened group scrolls into view once the
    // expanders have laid out.
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.SettingsSearchText) || _viewModel is null || !_viewModel.HasSettingsSearchText)
        {
            return;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ScrollFirstOpenGroupIntoView));
    }

    private void ScrollFirstOpenGroupIntoView()
    {
        var target = FindFirstOpenExpander(SettingsScrollViewer);
        target?.BringIntoView();
    }

    private static Expander? FindFirstOpenExpander(DependencyObject root)
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is Expander { IsExpanded: true, Visibility: Visibility.Visible } expander)
            {
                return expander;
            }

            var nested = FindFirstOpenExpander(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
