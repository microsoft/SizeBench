using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using SizeBench.GUI.Controls.Navigation;

namespace SizeBench.GUI.Pages;

[ExcludeFromCodeCoverage]
public partial class TemplateFoldabilityItemPage : SizeBenchPage
{
    public TemplateFoldabilityItemPage()
    {
        InitializeComponent();
        PreviewKeyDown += TemplateFoldabilityItemPage_PreviewKeyDown;
    }

    private void TemplateFoldabilityItemPage_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control || this.DataContext is not TemplateFoldabilityItemPageViewModel viewModel)
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
