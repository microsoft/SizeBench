using System.Diagnostics.CodeAnalysis;

namespace SizeBench.GUI.Controls.Navigation;

// This type is excluded from code coverage because it's basically a big copy/paste from the Silverlight SDK and in Silverlight
// this was extensively tested.  See details at the top of the file for SizeBenchFrame.
[ExcludeFromCodeCoverage]
public sealed class SizeBenchNavigationEventArgs : EventArgs
{
    #region Constructors

    internal SizeBenchNavigationEventArgs(object? content, Uri? uri)
    {
        this.Content = content;
        this.Uri = uri;
    }

    #endregion Constructors

    #region Properties

    public object? Content { get; }

    public Uri? Uri { get; }

    #endregion Properties
}
