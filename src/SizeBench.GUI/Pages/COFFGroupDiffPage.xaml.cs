using System.Diagnostics.CodeAnalysis;
using SizeBench.GUI.Controls.Navigation;

namespace SizeBench.GUI.Pages;

[ExcludeFromCodeCoverage]
public partial class COFFGroupDiffPage : SizeBenchPage
{
    public COFFGroupDiffPage()
    {
        InitializeComponent();
    }

    protected override void OnViewModelReady()
    {
        // Because DataGridTextColumn is just a DO I cannot seem to figure out how to use Binding to get this set up in markup.
        // Using ElementName and RelativeSource/FindAncestor can't find the ViewModel.  So, the only way I've been able to figure
        // out how to set this is in code-behind in the View :(
        this.libSizeColumn.SortMemberPath = ((COFFGroupDiffPageViewModel)this.DataContext).ContributionSizeSortMemberPath;
        this.compilandSizeColumn.SortMemberPath = this.libSizeColumn.SortMemberPath;

        this.libVirtualSizeColumn.SortMemberPath = ((COFFGroupDiffPageViewModel)this.DataContext).ContributionVirtualSizeSortMemberPath;
        this.compilandVirtualSizeColumn.SortMemberPath = this.libVirtualSizeColumn.SortMemberPath;
    }
}
