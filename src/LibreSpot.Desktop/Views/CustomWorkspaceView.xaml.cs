using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;
using System.Windows.Input;
using System.Windows.Media;
using LibreSpot.Desktop.ViewModels;

namespace LibreSpot.Desktop.Views;

public partial class CustomWorkspaceView : UserControl
{
    private static readonly Regex NumericInput = new("^[0-9]+$", RegexOptions.Compiled);
    private bool _syncingCustomPatchEditor;

    public CustomWorkspaceView()
    {
        InitializeComponent();
    }

    public FrameworkElement ProfileSurface => ProfileQaSurface;

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

    private void NestedScrollRegion_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta == 0 || sender is not DependencyObject source)
        {
            return;
        }

        var scrollViewer = FindDescendantScrollViewer(source);
        if (scrollViewer is not null && CanScrollVertically(scrollViewer, e.Delta))
        {
            return;
        }

        if (VisualTreeHelper.GetParent(source) is not UIElement parent)
        {
            return;
        }

        e.Handled = true;
        parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = sender
        });
    }

    private void CacheLimitTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !NumericInput.IsMatch(e.Text);

    private void CacheLimitTextBox_OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(typeof(string)))
        {
            e.CancelCommand();
            return;
        }

        var pasted = e.DataObject.GetData(typeof(string)) as string ?? string.Empty;
        if (!NumericInput.IsMatch(pasted))
        {
            e.CancelCommand();
        }
    }

    private static bool CanScrollVertically(ScrollViewer scrollViewer, int delta) =>
        delta < 0
            ? scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight
            : scrollViewer.VerticalOffset > 0;

    private static ScrollViewer? FindDescendantScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer scrollViewer)
        {
            return scrollViewer;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            var descendant = FindDescendantScrollViewer(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}
