using System.Windows.Automation.Peers;
using System.Windows.Controls;
namespace Murmur.App;
// Labels inside a ControlTemplate are normally excluded from UIA's control view.
// Expose the running version explicitly for assistive technology and verification.
public sealed class VersionLabel : TextBlock
{
    protected override AutomationPeer OnCreateAutomationPeer() => new VersionPeer(this);
    private sealed class VersionPeer(VersionLabel label) : FrameworkElementAutomationPeer(label)
    {
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
        protected override string GetNameCore() => label.Text;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
    }
}
