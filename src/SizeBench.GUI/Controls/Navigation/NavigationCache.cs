using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace SizeBench.GUI.Controls.Navigation;

// This type is excluded from code coverage because it's basically a big copy/paste from the Silverlight SDK and in Silverlight
// this was extensively tested.  See details at the top of the file for SizeBenchFrame.
[ExcludeFromCodeCoverage]
internal sealed class NavigationCache : DependencyObject
{
    #region Fields

    private int _cacheSize;
    private readonly Dictionary<string, SizeBenchPage> _cachePages;
    private readonly List<string> _cachePagesMRU;

    #endregion

    #region Properties

    //This property is for testing purposes only
    internal int CachePagesSize => this._cachePages.Count;

    //This property is for testing purposes only
    internal int CacheMRUPagesSize => this._cachePagesMRU.Count;

    internal SizeBenchPage? this[string uri]
    {
        get
        {
            if (this._cachePages.TryGetValue(uri, out var value))
            {
                return value;
            }

            return null;
        }
    }

    #endregion

    #region NavigationCacheMode Attached Property

    internal static readonly DependencyProperty NavigationCacheModeProperty = DependencyProperty.RegisterAttached(
            "NavigationCacheMode",
            typeof(NavigationCacheMode),
            typeof(NavigationCache),
            new PropertyMetadata(NavigationCacheMode.Disabled));

    internal static NavigationCacheMode GetNavigationCacheMode(DependencyObject depObj)
    {
        ArgumentNullException.ThrowIfNull(depObj);
        return (NavigationCacheMode)depObj.GetValue(NavigationCacheModeProperty);
    }

    internal static void SetNavigationCacheMode(DependencyObject depObj, NavigationCacheMode navigationCacheMode)
    {
        ArgumentNullException.ThrowIfNull(depObj);
        depObj.SetValue(NavigationCacheModeProperty, navigationCacheMode);
    }

    #endregion

    #region Constructor

    internal NavigationCache(int initialCacheSize)
    {
        this._cacheSize = initialCacheSize;
        this._cachePages = new Dictionary<string, SizeBenchPage>(this._cacheSize);
        this._cachePagesMRU = new List<string>(this._cacheSize);
    }

    #endregion

    #region Methods

    internal void ChangeCacheSize(int newCacheSize)
    {
        while (this._cachePagesMRU.Count > newCacheSize)
        {
            var toRemove = this._cachePagesMRU[^1];

            this._cachePagesMRU.RemoveAt(this._cachePagesMRU.Count - 1);
            this._cachePages.Remove(toRemove);
        }

        this._cacheSize = newCacheSize;
    }

    internal bool Contains(string uri) => this._cachePages.ContainsKey(uri);

    internal void AddToCache(string uri, SizeBenchPage page)
    {
#pragma warning disable CA1864 // Prefer the 'IDictionary.TryAdd(TKey, TValue)' method - this reads much clearer to me, and the perf hit here should be minimal as this isn't in a hot path
        if (this._cachePages.ContainsKey(uri))
        {
            // If it's already in the cache, bump it to the top,
            // and don't bother to examine the cache size, as we
            // are only moving stuff around, so size is not affected.
            this._cachePagesMRU.Remove(uri);
            this._cachePagesMRU.Insert(0, uri);
            this._cachePages[uri] = page;
        }
        else if (this._cacheSize > 0)
        {
            // If we're about to go over the size, instead remove the last entry before
            // adding the new one.
            if (this._cachePagesMRU.Count == this._cacheSize)
            {
                var toRemove = this._cachePagesMRU[^1];
                this._cachePagesMRU.RemoveAt(this._cachePagesMRU.Count - 1);
                this._cachePages.Remove(toRemove);
            }

            this._cachePagesMRU.Insert(0, uri);
            this._cachePages.Add(uri, page);
        }
#pragma warning restore CA1864 // Prefer the 'IDictionary.TryAdd(TKey, TValue)' method
    }

    internal void RemoveFromCache(string uri)
    {
        this._cachePagesMRU.Remove(uri);
        this._cachePages.Remove(uri);
    }

    #endregion
}
