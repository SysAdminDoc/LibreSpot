using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UserControl = System.Windows.Controls.UserControl;

namespace LibreSpot.Desktop.Views;

public partial class CustomLiveCustomizationSection : UserControl
{
    public CustomLiveCustomizationSection()
    {
        InitializeComponent();
    }

    private void NestedScrollRegion_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e) =>
        WorkspaceViewInteraction.BubbleMouseWheel(sender, e);

    private void NumericValueTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e) =>
        WorkspaceViewInteraction.FilterFeatureNumberTextInput(e);

    private void NumericValueTextBox_OnPasting(object sender, DataObjectPastingEventArgs e) =>
        WorkspaceViewInteraction.CancelInvalidFeatureNumberPaste(e);
}
