namespace SizeBench.GUI.Core;

internal static class IListOfTExtensions
{
    internal static void AddRange<T>(this IList<T> list, IEnumerable<T> toAdd)
    {
        foreach (var item in toAdd)
        {
            list.Add(item);
        }
    }
}
