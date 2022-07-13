using System.Collections.ObjectModel;

namespace SizeBench.GUI.Core;

internal static class ObservableCollectionExtensions
{
    internal static void AddRange<T>(this ObservableCollection<T> observableCollection, IEnumerable<T> toAdd)
    {
        foreach (var item in toAdd)
        {
            observableCollection.Add(item);
        }
    }
}
