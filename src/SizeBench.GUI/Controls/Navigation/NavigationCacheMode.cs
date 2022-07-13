namespace SizeBench.GUI.Controls.Navigation;

/// <summary>
/// Used to describe how a <see cref="SizeBenchPage"/> should be cached when
/// used by a <see cref="SizeBenchFrame"/>
/// </summary>
public enum NavigationCacheMode
{
    /// <summary>
    /// The <see cref="SizeBenchPage"/> should never be cached, and a new
    /// instance should be created on each navigation.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// The <see cref="SizeBenchPage"/> should always be cached, and kept
    /// around forever, reused in all subsequent navigations
    /// to the same Uri.
    /// </summary>
    Required = 1,

    /// <summary>
    /// The <see cref="SizeBenchPage"/> should be cached only within
    /// the size of the cache on the <see cref="SizeBenchFrame"/>,
    /// and thrown away if it would exceed that.
    /// </summary>
    Enabled = 2
}
