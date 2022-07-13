using System.Diagnostics.CodeAnalysis;
using SizeBench.GUI.Controls.Navigation;

namespace SizeBench.GUI.Pages.Symbols;

[ExcludeFromCodeCoverage]
public partial class FunctionCodeSymbolDiffPage : SizeBenchPage
{
    public FunctionCodeSymbolDiffPage()
    {
        InitializeComponent();
    }

    private void detailsScrollViewer_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
    {
        if (e.VerticalChange == 0 && e.HorizontalChange == 0)
        {
            return;
        }

        if (sender == this.beforeDetailsScrollViewer)
        {
            this.afterDetailsScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
            this.afterDetailsScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
        }
        else
        {
            this.beforeDetailsScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
            this.beforeDetailsScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
        }
    }
}
