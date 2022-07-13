using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace SizeBench.GUI.Controls.Navigation;

// This type is excluded from code coverage because it's basically a big copy/paste from the Silverlight SDK and in Silverlight
// this was extensively tested.  See details at the top of the file for SizeBenchFrame.
[ExcludeFromCodeCoverage]
public sealed class SizeBenchNavigatingCancelEventArgs : CancelEventArgs
{
    #region Constructors

    internal SizeBenchNavigatingCancelEventArgs(Uri uri, NavigationMode mode)
    {
        this.Uri = uri;
        this.NavigationMode = mode;
    }

    #endregion Constructors

    #region Properties

    public Uri Uri { get; }

    public NavigationMode NavigationMode { get; }

    #endregion Properties
}
