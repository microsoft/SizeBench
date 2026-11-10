using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using SizeBench.GUI.Controls.Navigation;

namespace SizeBench.GUI.Pages.Symbols;

[ExcludeFromCodeCoverage]
public partial class FunctionCodeSymbolDiffPage : SizeBenchPage
{
    public FunctionCodeSymbolDiffPage()
    {
        InitializeComponent();
        PreviewKeyDown += FunctionCodeSymbolDiffPage_PreviewKeyDown;
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

    private void FunctionCodeSymbolDiffPage_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control || this.DataContext is not FunctionCodeSymbolDiffPageViewModel viewModel)
        {
            return;
        }

        if (e.Key is Key.OemPlus or Key.Add)
        {
            viewModel.IncreaseDisassemblyZoom();
            e.Handled = true;
        }
        else if (e.Key is Key.OemMinus or Key.Subtract)
        {
            viewModel.DecreaseDisassemblyZoom();
            e.Handled = true;
        }
    }
}
