using System.Collections;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Data;
using SizeBench.TestInfrastructure;

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
        Assert.AreEqual(0, this.DataGrid.Columns.Count);

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
        Assert.AreEqual(0, this.DataGrid.Columns.Count);

        DataGridExtension.SetColumns(this.DataGrid, columns);

        Assert.AreEqual(5, this.DataGrid.Columns.Count);
        Assert.AreEqual(3, this.DataGrid.Columns.OfType<DataGridTextColumn>().Count());
        Assert.AreEqual(1, this.DataGrid.Columns.OfType<DataGridCheckBoxColumn>().Count());
        Assert.AreEqual(1, this.DataGrid.Columns.OfType<DataGridComboBoxColumn>().Count());
        Assert.IsInstanceOfType(this.DataGrid.Columns[2], typeof(DataGridComboBoxColumn));
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
        Assert.AreEqual(0, this.DataGrid.Columns.Count);

        DataGridExtension.SetColumns(this.DataGrid, columns);

        Assert.AreEqual(5, this.DataGrid.Columns.Count);
        Assert.AreEqual(3, this.DataGrid.Columns.OfType<DataGridTextColumn>().Count());
        Assert.AreEqual(1, this.DataGrid.Columns.OfType<DataGridCheckBoxColumn>().Count());
        Assert.AreEqual(1, this.DataGrid.Columns.OfType<DataGridComboBoxColumn>().Count());
        Assert.IsInstanceOfType(this.DataGrid.Columns[2], typeof(DataGridComboBoxColumn));

        // Try just adding a column to the main collection
        columns.Add(new DataGridTemplateColumn()); // Index 5
        Assert.AreEqual(6, this.DataGrid.Columns.Count);
        Assert.IsInstanceOfType(this.DataGrid.Columns[5], typeof(DataGridTemplateColumn));

        // Try adding to one of the CollectionContainers within
        ((columns[2] as CollectionContainer)!.Collection as ObservableCollection<DataGridColumn>)!.Add(new DataGridHyperlinkColumn()); // Index 6
        Assert.AreEqual(7, this.DataGrid.Columns.Count);
        Assert.IsInstanceOfType(this.DataGrid.Columns[6], typeof(DataGridHyperlinkColumn));

        // Try adding a CollectionContainer
        var newCollectionContainer = new CollectionContainer()
        {
            Collection = new ObservableCollection<DataGridColumn>() {
                    new DataGridComboBoxColumn(), // Index 7
                    new DataGridTextColumn() // Index 8
                }
        };
        columns.Add(newCollectionContainer);
        Assert.AreEqual(9, this.DataGrid.Columns.Count);
        Assert.IsInstanceOfType(this.DataGrid.Columns[7], typeof(DataGridComboBoxColumn));

        // Try adding to that new CollectionContainer
        (newCollectionContainer.Collection as ObservableCollection<DataGridColumn>)!.Add(new DataGridCheckBoxColumn()); // Index 9
        Assert.AreEqual(10, this.DataGrid.Columns.Count);
        Assert.IsInstanceOfType(this.DataGrid.Columns[9], typeof(DataGridCheckBoxColumn));

        // Try removing a column from the main collection
        columns.RemoveAt(3); // Removing the TextColumn after the CollectionContainer from the start, which is column 4 in DataGrid.Columns
        Assert.AreEqual(9, this.DataGrid.Columns.Count);
        Assert.AreEqual(3, this.DataGrid.Columns.OfType<DataGridTextColumn>().Count()); // Two of the original ones remain, plus the one in newCollectionContainer
        Assert.IsInstanceOfType(this.DataGrid.Columns[4], typeof(DataGridTemplateColumn)); // The collection 'scooted up'

        // Try removing a CollectionContainer
        //    -- this should let the object die since INCC should be unsubscribed, but that work isn't done yet, so that part
        //       of the test is commented out for now.
        columns.RemoveAt(4); // Removing the newCollectionContainer added above
        Assert.AreEqual(6, this.DataGrid.Columns.Count);
        Assert.AreEqual(2, this.DataGrid.Columns.OfType<DataGridTextColumn>().Count());
        Assert.AreEqual(1, this.DataGrid.Columns.OfType<DataGridComboBoxColumn>().Count());

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
