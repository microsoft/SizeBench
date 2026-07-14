using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using SizeBench.GUI.Controls.Navigation;

namespace SizeBench.GUI.Pages.Symbols;

[ExcludeFromCodeCoverage]
public partial class CodeBlockSymbolDiffPage : SizeBenchPage
{
    public CodeBlockSymbolDiffPage()
    {
        InitializeComponent();
        PreviewKeyDown += CodeBlockSymbolDiffPage_PreviewKeyDown;
    }

    private void CodeBlockSymbolDiffPage_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control || this.DataContext is not CodeBlockSymbolDiffPageViewModel viewModel)
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
