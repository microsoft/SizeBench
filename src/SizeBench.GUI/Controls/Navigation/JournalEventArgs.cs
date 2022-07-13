using System.Diagnostics.CodeAnalysis;

namespace SizeBench.GUI.Controls.Navigation;

// This type is excluded from code coverage because it's basically a big copy/paste from the Silverlight SDK and in Silverlight
// this was extensively tested.  See details at the top of the file for SizeBenchFrame.
[ExcludeFromCodeCoverage]
internal sealed class JournalEventArgs : EventArgs
{
    #region Constructors

    internal JournalEventArgs(string name, Uri uri, NavigationMode mode)
    {
        ArgumentNullException.ThrowIfNull(uri);

        this.Name = name;
        this.Uri = uri;
        this.NavigationMode = mode;
    }

    #endregion Constructors

    #region Properties

    internal string Name { get; }

    internal Uri Uri { get; }

    internal NavigationMode NavigationMode { get; }

    #endregion Properties
}
