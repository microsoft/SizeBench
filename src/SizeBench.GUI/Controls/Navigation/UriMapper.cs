using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Markup;

namespace SizeBench.GUI.Controls.Navigation;

// This type is excluded from code coverage because it's basically a big copy/paste from the Silverlight SDK and in Silverlight
// this was extensively tested.  See details at the top of the file for SizeBenchFrame.
[ExcludeFromCodeCoverage]
[ContentProperty("UriMappings")]
public sealed class UriMapper : UriMapperBase
{
    #region Constructors

    /// <summary>
    /// Default constructor.
    /// </summary>
    public UriMapper()
    {
        this.UriMappings = new Collection<UriMapping>();
    }

    #endregion

    #region Properties

    public Collection<UriMapping> UriMappings { get; }

    #endregion Properties

    #region Methods

    public override Uri MapUri(Uri uri)
    {
        var mappings = this.UriMappings;

        if (mappings is null)
        {
            throw new InvalidOperationException("UriMapper must not have a null collection of UriMappings");
        }

        ArgumentNullException.ThrowIfNull(uri);

        Uri? mappedUri;

        foreach (var mapping in mappings)
        {
            mappedUri = mapping.MapUri(uri);
            if (mappedUri != null)
            {
                return mappedUri;
            }
        }

        // If no mapping was able to process the uri, return the original
        return uri;
    }

    #endregion Methods
}
