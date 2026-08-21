using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UserControl = System.Windows.Controls.UserControl;

namespace LibreSpot.Desktop.Views;

public partial class CustomProfileSummarySection : UserControl
{
    public CustomProfileSummarySection()
    {
        InitializeComponent();
    }

    public FrameworkElement ProfileSurface => ProfileQaSurface;

    private void NestedScrollRegion_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e) =>
        WorkspaceViewInteraction.BubbleMouseWheel(sender, e);
}
