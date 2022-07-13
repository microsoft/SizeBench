using System.Diagnostics.CodeAnalysis;
using System.Windows.Automation.Peers;

namespace SizeBench.GUI.Controls.Navigation;

// This type is excluded from code coverage because it's basically a big copy/paste from the Silverlight SDK and in Silverlight
// this was extensively tested.  See details at the top of the file for SizeBenchFrame.
[ExcludeFromCodeCoverage]
public class SizeBenchFrameAutomationPeer : FrameworkElementAutomationPeer
{
    public SizeBenchFrameAutomationPeer(SizeBenchFrame owner)
        : base(owner)
    {
    }

    #region AutomationPeer overrides

    protected override AutomationControlType GetAutomationControlTypeCore()
        => AutomationControlType.Pane;

    protected override string GetClassNameCore()
        => this.Owner.GetType().Name;

    protected override string GetNameCore()
    {
        var name = base.GetNameCore();

        if (String.IsNullOrEmpty(name))
        {
            name = ((SizeBenchFrame)this.Owner).Name;
        }

        if (String.IsNullOrEmpty(name))
        {
            name = GetClassName();
        }

        return name;
    }

    #endregion AutomationPeer overrides
}
