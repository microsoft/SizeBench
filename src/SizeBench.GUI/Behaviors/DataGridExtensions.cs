using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SizeBench.GUI.Behaviors;

//TODO: There's a bunch of latent bugs in this code when collection changes occur - we don't properly unsubscribe from INCC.CollectionChanged
//      in a whole bunch of cases, so this probably encourages leaks.  We also don't properly subscribe to INCC.CollectionChanged on newly-added
//      things.
//      For now this seems to not be hit in the UI, and it's kinda hard to plumb it all, so leaving it sorta broken for now, just leaving this
//      as a reminder that someday this should get cleaned up to be better.
public static class DataGridExtension
{
    public static IList GetColumns(DependencyObject depObj)
    {
        ArgumentNullException.ThrowIfNull(depObj);

        return (IList)depObj.GetValue(ColumnsProperty);
    }

    public static void SetColumns(DependencyObject depObj, IList value)
    {
        ArgumentNullException.ThrowIfNull(depObj);

        depObj.SetValue(ColumnsProperty, value);
    }

    public static readonly DependencyProperty ColumnsProperty =
           DependencyProperty.RegisterAttached("Columns",
           typeof(IList),
           typeof(DataGridExtension),
           new UIPropertyMetadata(null, OnDataGridColumnsPropertyChanged));

    private static void OnDataGridColumnsPropertyChanged(
           DependencyObject d,
           DependencyPropertyChangedEventArgs e)
    {
        if (d.GetType() == typeof(DataGrid))
        {
            var myGrid = (DataGrid)d;

            if (e.OldValue as INotifyCollectionChanged != null)
            {
                // Too lazy to write the code now to support re-binding to the collection, requires thinking about how to
                // unhook the INCC handlers for the collection and each CollectionContainer inside it.
                throw new NotImplementedException();
            }
            var Columns = (IList)e.NewValue;

            if (Columns != null)
            {
                myGrid.Columns.Clear();

                if (Columns.Count > 0)
                {
                    for (var i = 0; i < Columns.Count; i++)
                    {
                        if (Columns[i] is DataGridColumn)
                        {
                            myGrid.Columns.Add(Columns[i] as DataGridColumn);
                        }
                        else if (Columns[i] is CollectionContainer containedCollection)
                        {
                            if (containedCollection.Collection != null)
                            {
                                if (containedCollection.Collection is INotifyCollectionChanged ccAsINCC)
                                {
                                    ccAsINCC.CollectionChanged += (sender, args) => ColumnCollectionChanged(myGrid, args);
                                }

                                foreach (DataGridColumn? column in containedCollection.Collection)
                                {
                                    if (column != null)
                                    {
                                        myGrid.Columns.Add(column);
                                    }
                                }
                            }
                        }
                    }
                }

                if (Columns is INotifyCollectionChanged columnsAsINCC)
                {
                    columnsAsINCC.CollectionChanged += (sender, args) => ColumnCollectionChanged(myGrid, args);
                }
            }
        }
    }

    private static void ColumnCollectionChanged(DataGrid myGrid, NotifyCollectionChangedEventArgs args)
    {
        if (args.NewItems != null)
        {
            for (var i = 0; i < args.NewItems.Count; i++)
            {
                if (args.NewItems[i] is DataGridColumn)
                {
                    myGrid.Columns.Add(args.NewItems[i] as DataGridColumn);
                }
                else if (args.NewItems[i] is CollectionContainer containedCollection)
                {
                    if (containedCollection.Collection is INotifyCollectionChanged ccAsINCC)
                    {
                        ccAsINCC.CollectionChanged += (s, e) => ColumnCollectionChanged(myGrid, e);
                    }

                    foreach (DataGridColumn? column in containedCollection.Collection)
                    {
                        if (column != null)
                        {
                            myGrid.Columns.Add(column);
                        }
                    }
                }
            }
        }

        if (args.OldItems != null)
        {
            for (var i = 0; i < args.OldItems.Count; i++)
            {
                if (args.OldItems[i] is DataGridColumn)
                {
                    myGrid.Columns.Remove(args.OldItems[i] as DataGridColumn);
                }
                else if (args.OldItems[i] is CollectionContainer containedCollection)
                {
                    if (containedCollection.Collection is INotifyCollectionChanged ccAsINCC)
                    {
                        ccAsINCC.CollectionChanged -= (s, e) => ColumnCollectionChanged(myGrid, e);
                    }

                    foreach (DataGridColumn? column in containedCollection.Collection)
                    {
                        if (column != null)
                        {
                            myGrid.Columns.Remove(column);
                        }
                    }
                }
            }
        }
    }
}
