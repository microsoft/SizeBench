using System.Diagnostics.CodeAnalysis;
using System.Windows.Controls;

namespace SizeBench.GUI.Controls.Errors;

// Pure view code, no need to test
[ExcludeFromCodeCoverage]
public partial class ErrorControl : UserControl
{
    public ErrorControl()
    {
        InitializeComponent();
    }
}
