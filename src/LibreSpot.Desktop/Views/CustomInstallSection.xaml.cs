using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UserControl = System.Windows.Controls.UserControl;

namespace LibreSpot.Desktop.Views;

public partial class CustomInstallSection : UserControl
{
    public CustomInstallSection()
    {
        InitializeComponent();
    }

    private void CacheLimitTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e) =>
        WorkspaceViewInteraction.FilterNumericTextInput(e);

    private void CacheLimitTextBox_OnPasting(object sender, DataObjectPastingEventArgs e) =>
        WorkspaceViewInteraction.CancelInvalidPaste(e);
}
