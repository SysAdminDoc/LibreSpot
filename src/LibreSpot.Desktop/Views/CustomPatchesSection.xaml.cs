using System;
using System.Windows.Controls;
using System.Windows.Input;
using LibreSpot.Desktop.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace LibreSpot.Desktop.Views;

public partial class CustomPatchesSection : UserControl
{
    private bool _syncingCustomPatchEditor;

    public CustomPatchesSection()
    {
        InitializeComponent();
    }

    public void SyncCustomPatchesEditorText(MainViewModel viewModel)
    {
        if (!IsLoaded)
        {
            return;
        }

        var next = viewModel.CustomPatchesJson ?? string.Empty;
        if (string.Equals(CustomPatchesTextEditor.Text, next, StringComparison.Ordinal))
        {
            return;
        }

        _syncingCustomPatchEditor = true;
        try
        {
            CustomPatchesTextEditor.Text = next;
        }
        finally
        {
            _syncingCustomPatchEditor = false;
        }
    }

    private void CustomPatchesTextEditor_OnTextChanged(object? sender, EventArgs e)
    {
        if (_syncingCustomPatchEditor)
        {
            return;
        }

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.CustomPatchesJson = CustomPatchesTextEditor.Text ?? string.Empty;
        }
    }

    private void NestedScrollRegion_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e) =>
        WorkspaceViewInteraction.BubbleMouseWheel(sender, e);
}
