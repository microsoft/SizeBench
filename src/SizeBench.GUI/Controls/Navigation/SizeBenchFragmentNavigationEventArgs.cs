using System.Diagnostics.CodeAnalysis;

namespace SizeBench.GUI.Controls.Navigation;

// This type is excluded from code coverage because it's basically a big copy/paste from the Silverlight SDK and in Silverlight
// this was extensively tested.  See details at the top of the file for SizeBenchFrame.
[ExcludeFromCodeCoverage]
public sealed class SizeBenchFragmentNavigationEventArgs : EventArgs
{
    #region Constructors

    internal SizeBenchFragmentNavigationEventArgs(string fragment)
    {
        this.Fragment = fragment;
    }

    #endregion Constructors

    #region Public Properties

    /// <summary>
    ///  The fragment part of the URI that was passed to the Navigate() API which initiated this navigation.
    ///  The fragment may be String.Empty.
    /// </summary>
    public string Fragment { get; }

    #endregion Public Properties
}
