using System.Windows;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;

namespace LibreSpot.Desktop.Views;

public partial class MaintenanceWorkspaceView : UserControl
{
    public MaintenanceWorkspaceView()
    {
        InitializeComponent();
    }

    public FrameworkElement SupportBundleSurface => SupportBundleQaSurface;
}
