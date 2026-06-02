using System.Diagnostics.CodeAnalysis;
using System.Windows.Controls;

namespace SizeBench.GUI.Windows;

// This is purely view code, there's no good way to test this currently
[ExcludeFromCodeCoverage]
public partial class SelectSessionOptionsControl : UserControl
{
    public SelectSessionOptionsControl()
    {
        InitializeComponent();
    }
}
