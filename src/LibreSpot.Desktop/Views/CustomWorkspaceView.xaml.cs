using System.Windows;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;
using LibreSpot.Desktop.ViewModels;

namespace LibreSpot.Desktop.Views;

public partial class CustomWorkspaceView : UserControl
{
    public CustomWorkspaceView()
    {
        InitializeComponent();
    }

    public FrameworkElement ProfileSurface => ProfileSummarySection.ProfileSurface;

    public void SyncCustomPatchesEditorText(MainViewModel viewModel) =>
        CustomPatchesSection.SyncCustomPatchesEditorText(viewModel);
}
