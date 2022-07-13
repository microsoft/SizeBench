using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace SizeBench.GUI.Controls.Navigation;

// This type is excluded from code coverage because it's basically a big copy/paste from the Silverlight SDK and in Silverlight
// this was extensively tested.  See details at the top of the file for SizeBenchFrame.
/// <summary>
/// Default implementation of INavigationContentLoader that is capable of resolving URI values to XAML types located in the package.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class PageResourceContentLoader : INavigationContentLoader
{
    #region Methods

    #region INavigationContentLoader implementation

    public Task<LoadResult> LoadContentAsync(Uri targetUri, Uri? currentUri)
    {
        ArgumentNullException.ThrowIfNull(targetUri);

        return Task.FromResult(new LoadResult(Application.LoadComponent(targetUri)));
    }

    /// <summary>
    /// Tells whether or not the target Uri can be loaded
    /// </summary>
    /// <param name="targetUri">A URI to load</param>
    /// <param name="currentUri">The current URI</param>
    /// <returns>True if the targetURI can be loaded</returns>
    public bool CanLoad(Uri targetUri, Uri? currentUri)
        => UriParsingHelper.InternalUriIsNavigable(targetUri);

    #endregion INavigationContentLoader implementation

    #endregion Methods
}
