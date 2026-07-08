using System.Collections;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Data;

namespace SizeBench.GUI.Behaviors.Tests;

[STATestClass]
public sealed class DataGridExtensionTests
{
    DataGrid DataGrid = new DataGrid();

    [TestInitialize]
    public void TestInitialize()
    {
        this.DataGrid = new DataGrid();
    }

    [TestMethod]
    public void ExtensionColumnsAttachedPropertyWorks()
    {
        var columns = new List<DataGridColumn>()
            {
                new DataGridTextColumn(),
                new DataGridCheckBoxColumn(),
                new DataGridTextColumn(),
            };

        Assert.IsNull(DataGridExtension.GetColumns(this.DataGrid));
        Assert.IsEmpty(this.DataGrid.Columns);

        DataGridExtension.SetColumns(this.DataGrid, columns);

        CollectionAssert.AreEqual(columns, this.DataGrid.Columns);
    }

    [TestMethod]
    public void CollectionContainersAreUnpacked()
    {
        var columns = new ArrayList()
            {
                new DataGridTextColumn(),
                new DataGridCheckBoxColumn(),
                new CollectionContainer() {
                    Collection = new List<DataGridColumn>() {
                        new DataGridComboBoxColumn(),
                        new DataGridTextColumn()
                    }
                },
                new DataGridTextColumn(),
            };

        Assert.IsNull(DataGridExtension.GetColumns(this.DataGrid));
        Assert.IsEmpty(this.DataGrid.Columns);

        DataGridExtension.SetColumns(this.DataGrid, columns);

        Assert.HasCount(5, this.DataGrid.Columns);
        Assert.HasCount(3, this.DataGrid.Columns.OfType<DataGridTextColumn>());
        Assert.HasCount(1, this.DataGrid.Columns.OfType<DataGridCheckBoxColumn>());
        Assert.HasCount(1, this.DataGrid.Columns.OfType<DataGridComboBoxColumn>());
        Assert.IsInstanceOfType<DataGridComboBoxColumn>(this.DataGrid.Columns[2]);
    }

    [TestMethod]
    public void CollectionChangesAreObserved()
    {
        var columns = new ObservableCollection<object>()
            {
                new DataGridTextColumn(), // Index 0
                new DataGridCheckBoxColumn(), // Index 1
                new CollectionContainer() {
                    Collection = new ObservableCollection<DataGridColumn>() {
                        new DataGridComboBoxColumn(), // Index 2
                        new DataGridTextColumn() // Index 3
                    }
                },
                new DataGridTextColumn(), // Index 4
            };

        Assert.IsNull(DataGridExtension.GetColumns(this.DataGrid));
        Assert.IsEmpty(this.DataGrid.Columns);

        DataGridExtension.SetColumns(this.DataGrid, columns);

        Assert.HasCount(5, this.DataGrid.Columns);
        Assert.HasCount(3, this.DataGrid.Columns.OfType<DataGridTextColumn>());
        Assert.HasCount(1, this.DataGrid.Columns.OfType<DataGridCheckBoxColumn>());
        Assert.HasCount(1, this.DataGrid.Columns.OfType<DataGridComboBoxColumn>());
        Assert.IsInstanceOfType<DataGridComboBoxColumn>(this.DataGrid.Columns[2]);

        // Try just adding a column to the main collection
        columns.Add(new DataGridTemplateColumn()); // Index 5
        Assert.HasCount(6, this.DataGrid.Columns);
        Assert.IsInstanceOfType<DataGridTemplateColumn>(this.DataGrid.Columns[5]);

        // Try adding to one of the CollectionContainers within
        ((columns[2] as CollectionContainer)!.Collection as ObservableCollection<DataGridColumn>)!.Add(new DataGridHyperlinkColumn()); // Index 6
        Assert.HasCount(7, this.DataGrid.Columns);
        Assert.IsInstanceOfType<DataGridHyperlinkColumn>(this.DataGrid.Columns[6]);

        // Try adding a CollectionContainer
        var newCollectionContainer = new CollectionContainer()
        {
            Collection = new ObservableCollection<DataGridColumn>() {
                    new DataGridComboBoxColumn(), // Index 7
                    new DataGridTextColumn() // Index 8
                }
        };
        columns.Add(newCollectionContainer);
        Assert.HasCount(9, this.DataGrid.Columns);
        Assert.IsInstanceOfType<DataGridComboBoxColumn>(this.DataGrid.Columns[7]);

        // Try adding to that new CollectionContainer
        (newCollectionContainer.Collection as ObservableCollection<DataGridColumn>)!.Add(new DataGridCheckBoxColumn()); // Index 9
        Assert.HasCount(10, this.DataGrid.Columns);
        Assert.IsInstanceOfType<DataGridCheckBoxColumn>(this.DataGrid.Columns[9]);

        // Try removing a column from the main collection
        columns.RemoveAt(3); // Removing the TextColumn after the CollectionContainer from the start, which is column 4 in DataGrid.Columns
        Assert.HasCount(9, this.DataGrid.Columns);
        Assert.HasCount(3, this.DataGrid.Columns.OfType<DataGridTextColumn>()); // Two of the original ones remain, plus the one in newCollectionContainer
        Assert.IsInstanceOfType<DataGridTemplateColumn>(this.DataGrid.Columns[4]); // The collection 'scooted up'

        // Try removing a CollectionContainer
        //    -- this should let the object die since INCC should be unsubscribed, but that work isn't done yet, so that part
        //       of the test is commented out for now.
        columns.RemoveAt(4); // Removing the newCollectionContainer added above
        Assert.HasCount(6, this.DataGrid.Columns);
        Assert.HasCount(2, this.DataGrid.Columns.OfType<DataGridTextColumn>());
        Assert.HasCount(1, this.DataGrid.Columns.OfType<DataGridComboBoxColumn>());

        // TODO: This part of the test is unstable, at some point someone should investigate why.
        //var newCollectionWeakRef = new WeakReference<CollectionContainer>(newCollectionContainer);
        //newCollectionContainer = null;
        //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        //GC.WaitForPendingFinalizers();
        //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        //GC.WaitForPendingFinalizers();
        // newCollectionContainer should be dead now since we should have unsubscribed from INCC.CollectionChanged
        //Assert.IsFalse(newCollectionWeakRef.TryGetTarget(out _));
    }
}
