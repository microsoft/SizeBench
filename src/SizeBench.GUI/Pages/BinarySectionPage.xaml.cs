using System.Diagnostics.CodeAnalysis;
using System.Windows.Data;
using SizeBench.GUI.Controls.Navigation;

namespace SizeBench.GUI.Pages;

[ExcludeFromCodeCoverage]
public partial class BinarySectionPage : SizeBenchPage
{
    public BinarySectionPage()
    {
        InitializeComponent();
    }

    protected override void OnViewModelReady()
    {
        // Because the ViewModel initializes async, it happens too late for the CompositeCollection to see
        // the CollectionContainer.Collection.  So we need to 'kick' it by removing and re-adding the last
        // item to convince the converter to go re-run column creation.
        KickCompositeCollection("compilandsColumns");
        KickCompositeCollection("libsColumns");
    }

    private void KickCompositeCollection(string resourceKeyOfCompositeCollection)
    {
        var columnCollection = (CompositeCollection)this.Resources[resourceKeyOfCompositeCollection];
        var lastItem = columnCollection[^1];
        columnCollection.Remove(lastItem);
        columnCollection.Add(lastItem);
    }
}
