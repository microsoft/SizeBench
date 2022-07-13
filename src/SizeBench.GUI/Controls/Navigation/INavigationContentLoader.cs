namespace SizeBench.GUI.Controls.Navigation;

// This type is excluded from code coverage because it's basically a big copy/paste from the Silverlight SDK and in Silverlight
// this was extensively tested.  See details at the top of the file for SizeBenchFrame.
public interface INavigationContentLoader
{
    Task<LoadResult> LoadContentAsync(Uri targetUri, Uri? currentUri);

    /// <summary>
    /// Tells whether or not the targetUri is of the correct format for <see cref="LoadContent(Uri,Uri)"/>.
    /// </summary>
    /// <param name="targetUri">A URI to load</param>
    /// <param name="currentUri">The current URI</param>
    /// <returns>True if the targetUri can be loaded</returns>
    bool CanLoad(Uri targetUri, Uri? currentUri);
}
