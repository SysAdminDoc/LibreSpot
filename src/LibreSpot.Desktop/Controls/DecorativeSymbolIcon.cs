using System.Windows.Automation.Peers;
using Wpf.Ui.Controls;

namespace LibreSpot.Desktop.Controls;

/// <summary>
/// A <see cref="SymbolIcon"/> that stays out of the automation tree.
/// </summary>
/// <remarks>
/// Wpf.Ui builds an icon from a <c>TextBlock</c> it adds as a visual child, and
/// the glyph it holds is a private-use character from the symbol font.
/// <c>IconElement</c> is a bare <c>FrameworkElement</c> and creates no peer, so
/// WPF walks past the icon and reaches the TextBlock, which does create one.
/// That is why the glyph arrived at UIA as <c>Text(50020)</c> holding an
/// unreadable name. The control beside each glyph carries the real one.
///
/// Setting <c>AutomationProperties.Name</c> on the icon does not reach the
/// TextBlock, because the TextBlock is a visual child rather than a templated
/// part. Returning a peer that reports no children is what stops the walk.
/// </remarks>
public sealed class DecorativeSymbolIcon : SymbolIcon
{
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new DecorativeAutomationPeer(this);

    private sealed class DecorativeAutomationPeer(DecorativeSymbolIcon owner)
        : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(DecorativeSymbolIcon);

        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;

        protected override List<AutomationPeer> GetChildrenCore() => [];
    }
}
