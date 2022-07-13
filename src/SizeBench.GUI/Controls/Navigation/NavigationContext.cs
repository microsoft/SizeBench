using System.Diagnostics.CodeAnalysis;

namespace SizeBench.GUI.Controls.Navigation;

// This type is excluded from code coverage because it's basically a big copy/paste from the Silverlight SDK and in Silverlight
// this was extensively tested.  See details at the top of the file for SizeBenchFrame.
[ExcludeFromCodeCoverage]
public sealed class NavigationContext
{
    #region Constructors

    internal NavigationContext(IDictionary<string, string> queryString)
    {
        this.QueryString = queryString;
    }

    #endregion Constructors

    #region Properties

    public IDictionary<string, string> QueryString { get; }

    #endregion Properties
}
