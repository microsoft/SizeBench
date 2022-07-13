using System.Diagnostics.CodeAnalysis;

namespace SizeBench.GUI.Controls.Navigation;

// This type is excluded from code coverage because it's basically a big copy/paste from the Silverlight SDK and in Silverlight
// this was extensively tested.  See details at the top of the file for SizeBenchFrame.
[ExcludeFromCodeCoverage]
public sealed class SizeBenchNavigationFailedEventArgs : EventArgs
{
    #region All Constructors

    internal SizeBenchNavigationFailedEventArgs(Uri? uri, Exception error)
    {
        this.Uri = uri;
        this.Exception = error;
    }

    #endregion All Constructors

    #region Properties

    public Uri? Uri { get; }

    public Exception Exception { get; }

    public bool Handled { get; set; }

    #endregion Properties
}
