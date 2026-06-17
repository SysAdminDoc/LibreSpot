using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace LibreSpot.Desktop.Controls;

public sealed class LiveRegionContentControl : ContentControl
{
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new LiveRegionContentControlAutomationPeer(this);

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);

        if (!AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
        {
            return;
        }

        var peer = UIElementAutomationPeer.FromElement(this) ??
                   UIElementAutomationPeer.CreatePeerForElement(this);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private sealed class LiveRegionContentControlAutomationPeer(LiveRegionContentControl owner)
        : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() =>
            AutomationControlType.Text;

        protected override string GetClassNameCore() => nameof(LiveRegionContentControl);

        protected override AutomationLiveSetting GetLiveSettingCore() =>
            AutomationLiveSetting.Polite;
    }
}
