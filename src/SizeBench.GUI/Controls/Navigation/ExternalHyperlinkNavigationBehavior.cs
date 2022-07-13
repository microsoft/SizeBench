using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Documents;
using System.Windows.Navigation;
using Microsoft.Xaml.Behaviors;

namespace SizeBench.GUI.Controls.Navigation;

[ExcludeFromCodeCoverage] // It's very hard to test process launches deterministically and reliably, and there's so little code here, not worth bothering to test.
public sealed class ExternalHyperlinkNavigationBehavior : Behavior<Hyperlink>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        this.AssociatedObject.RequestNavigate += OnAssociatedObjectRequestNavigate;
        this.AssociatedObject.ToolTip = this.AssociatedObject.NavigateUri;
    }
    protected override void OnDetaching()
    {
        this.AssociatedObject.RequestNavigate -= OnAssociatedObjectRequestNavigate;
        base.OnDetaching();
    }

    private void OnAssociatedObjectRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo()
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
        e.Handled = true;
    }
}
