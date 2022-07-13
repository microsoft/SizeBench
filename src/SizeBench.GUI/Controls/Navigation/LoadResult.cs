using System.Diagnostics.CodeAnalysis;

namespace SizeBench.GUI.Controls.Navigation;

// This type is excluded from code coverage because it's basically a big copy/paste from the Silverlight SDK and in Silverlight
// this was extensively tested.  See details at the top of the file for SizeBenchFrame.
/// <summary>
/// Result of a Load operation from an <see cref="INavigationContentLoader"/>
/// </summary>
[ExcludeFromCodeCoverage]
public class LoadResult
{
    /// <summary>
    /// Creates a LoadResult
    /// </summary>
    /// <param name="loadedContent">Content loaded from the load operation</param>
    public LoadResult(object loadedContent)
    {
        this.LoadedContent = loadedContent;
    }

    /// <summary>
    /// Content loaded from the load operation
    /// </summary>
    public object LoadedContent { get; }
}
