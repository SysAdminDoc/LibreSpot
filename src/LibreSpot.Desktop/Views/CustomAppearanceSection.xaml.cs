using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UserControl = System.Windows.Controls.UserControl;

namespace LibreSpot.Desktop.Views;

public partial class CustomAppearanceSection : UserControl
{
    public CustomAppearanceSection()
    {
        InitializeComponent();
    }

    private void NestedScrollRegion_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e) =>
        WorkspaceViewInteraction.BubbleMouseWheel(sender, e);

    private void CacheLimitTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e) =>
        WorkspaceViewInteraction.FilterNumericTextInput(e);

    private void CacheLimitTextBox_OnPasting(object sender, DataObjectPastingEventArgs e) =>
        WorkspaceViewInteraction.CancelInvalidPaste(e);
}
