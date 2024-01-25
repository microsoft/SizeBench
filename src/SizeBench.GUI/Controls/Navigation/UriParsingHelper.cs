using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Web;

namespace SizeBench.GUI.Controls.Navigation;

// This type is excluded from code coverage because it's basically a big copy/paste from the Silverlight SDK and in Silverlight
// this was extensively tested.  See details at the top of the file for SizeBenchFrame.
[ExcludeFromCodeCoverage]
internal static class UriParsingHelper
{
    #region Static fields and constants

    internal const char QueryStringDelimiter = '?';
    private const string ValueDelimiter = "=";
    private const string StatePairDelimiter = "&";
    private const string FragmentDelimiter = "#";
    private const string PathSeparator = "/";
    private const string HttpLocalhost = "http://localhost";
    private const string XamlExtension = ".xaml";

    internal const string ComponentDelimiter = ";component/";
    internal const string ComponentDelimiterWithoutSlash = ";component";
    internal static readonly int ComponentDelimiterWithoutSlashLength = ComponentDelimiterWithoutSlash.Length;

    #endregion Static fields and constants

    #region Methods

    #region Methods acting on internal Uris

    private static Uri MakeAbsolute(Uri? baseUri)
    {
        if (baseUri is null || baseUri.OriginalString.StartsWith(PathSeparator, StringComparison.Ordinal))
        {
            return new Uri(HttpLocalhost + baseUri, UriKind.Absolute);
        }
        else
        {
            return new Uri(HttpLocalhost + PathSeparator + baseUri, UriKind.Absolute);
        }
    }

    private static string GetUriComponents(Uri? uri, UriComponents components)
    {
        if (uri != null && String.IsNullOrEmpty(uri.OriginalString))
        {
            return String.Empty;
        }

        if (uri != null && uri.OriginalString.StartsWith(PathSeparator, StringComparison.Ordinal))
        {
            components |= UriComponents.KeepDelimiter;
        }

        return MakeAbsolute(uri).GetComponents(components, UriFormat.SafeUnescaped);
    }

    internal static Uri InternalUriMerge(Uri? baseUri, Uri newUri)
    {
        ArgumentNullException.ThrowIfNull(newUri);

        baseUri ??= new Uri(String.Empty, UriKind.Relative);

        Debug.Assert(!InternalUriIsFragment(baseUri), "Cannot merge URIs when the base Uri is only a fragment");

        // If the newUri is just a fragment, this is easy
        if (InternalUriIsFragment(newUri))
        {
            if (baseUri.OriginalString.StartsWith(PathSeparator, StringComparison.Ordinal))
            {
                return new Uri(InternalUriGetAllButFragment(baseUri) + newUri.OriginalString, UriKind.Relative);
            }
            else
            {
                // Account for the case when baseUri.OriginalString == String.Empty, which can happen
                // when a Frame is initially loaded
                var baseAllButFragment = InternalUriGetAllButFragment(baseUri);
                if (!String.IsNullOrEmpty(baseAllButFragment))
                {
                    baseAllButFragment = baseAllButFragment[1..];
                }

                return new Uri(baseAllButFragment + newUri.OriginalString, UriKind.Relative);
            }
        }

        return newUri;
    }

    internal static bool InternalUriIsNavigable(Uri? uri)
    {
        return uri != null &&
               (InternalUriIsFragment(uri) ||  // Fragment uri or non-fragment uri with a xaml extension
                   ((InternalUriIsRelativeToAppRoot(uri) || InternalUriIsRelativeWithComponent(uri) || String.IsNullOrEmpty(uri.OriginalString)) &&
                    InternalUriHasXamlExtension(uri)));
    }

    internal static bool InternalUriHasXamlExtension(Uri? uri)
    {
        var path = InternalUriGetPath(uri);
        if (path != null)
        {
            return path.EndsWith(XamlExtension, StringComparison.Ordinal);
        }
        return false;
    }

    internal static bool InternalUriIsRelativeToAppRoot(Uri uri)
    {
        return !uri.IsAbsoluteUri &&
               uri.OriginalString.StartsWith(PathSeparator, StringComparison.Ordinal) && // If the OriginalString does not start with "/" then it is not relative to the app *root*
               !uri.OriginalString.Contains(ComponentDelimiter, StringComparison.Ordinal);  // If the OriginalString contains ";component/" then it is not relative to the app root - it is relative to another assembly
    }

    internal static bool InternalUriIsRelativeWithComponent(Uri uri)
    {
        if (uri.OriginalString.Length < 1 ||
            uri.IsAbsoluteUri ||
            uri.OriginalString.StartsWith(PathSeparator, StringComparison.OrdinalIgnoreCase) == false) // If the Uri doesn't start with "/" then it's not relative in a manner we can use
        {
            return false;
        }

        // Copied directly from System.Windows.Application.IsComponentUri(Uri xamlUri)
        var str = uri.ToString();
        var startIndex = 0;

        if (str[0] == PathSeparator[0])
        {
            startIndex = 1;
        }

        var index = str.IndexOf(PathSeparator[0], startIndex);

        if ((index > 0) && str[startIndex..index].EndsWith(ComponentDelimiterWithoutSlash, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Parses the Uri to determine if it is a fragment
    /// </summary>
    /// <param name="uri">The uri to parse</param>
    /// <returns>True if this Uri is a fragment, false if it is not</returns>
    internal static bool InternalUriIsFragment(Uri? uri)
    {
        return uri != null &&
               !uri.IsAbsoluteUri &&
               !String.IsNullOrEmpty(uri.OriginalString) &&
               uri.OriginalString.StartsWith(FragmentDelimiter, StringComparison.Ordinal);
    }

    /// <summary>
    /// Parses the Uri to retrieve the fragment, if present
    /// </summary>
    /// <param name="uri">The uri to parse</param>
    /// <returns>The fragment, or null if there is not one</returns>
    internal static string InternalUriGetFragment(Uri? uri)
        => MakeAbsolute(uri).GetComponents(UriComponents.Fragment, UriFormat.Unescaped);

    /// <summary>
    /// Parses the Uri to strip off the fragment
    /// </summary>
    /// <param name="uri">The uri to parse</param>
    /// <returns>The uri without the fragment</returns>
    internal static string InternalUriGetAllButFragment(Uri? uri)
        => GetUriComponents(uri, UriComponents.PathAndQuery);

    /// <summary>
    /// Parses the Uri to strip off the query string and the fragment
    /// </summary>
    /// <param name="uri">The uri to parse</param>
    /// <returns>The uri without the query string or the fragment</returns>
    internal static string InternalUriGetPath(Uri? uri)
        => GetUriComponents(uri, UriComponents.Path);

    /// <summary>
    /// Parse the query string out of a Uri (the part following the '?')
    /// </summary>
    /// <param name="uri">The uri to parse for a query string</param>
    /// <returns>The query string, without a leading '?'.  Empty string in the case of no query string present.</returns>
    internal static string InternalUriGetQueryString(Uri? uri)
        => MakeAbsolute(uri).GetComponents(UriComponents.Query, UriFormat.SafeUnescaped);

    /// <summary>
    /// Cut the query string off a given Uri, to process only the part before the '?', and strips off the fragment
    /// </summary>
    /// <param name="uri">The uri to parse</param>
    /// <returns>The uri without its query string, and without its fragment</returns>
    internal static string InternalUriGetBaseValue(Uri uri)
    {
        var components = UriComponents.Path;

        if (uri.OriginalString.StartsWith('/'))
        {
            components |= UriComponents.KeepDelimiter;
        }

        return MakeAbsolute(uri).GetComponents(components, UriFormat.SafeUnescaped);
    }

    /// <summary>
    /// Parses the query string into name/value pairs
    /// </summary>
    /// <param name="uri">The Uri to parse the query string from</param>
    /// <param name="decodeResults">True if the resulting dictionary should contain decoded values, false if not</param>
    /// <returns>A dictionary containing one entry for each name/value pair in the query string</returns>
    internal static Dictionary<string, string> InternalUriParseQueryStringToDictionary(Uri? uri, bool decodeResults)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);

        var kvps = InternalUriGetQueryString(uri).Split(StatePairDelimiter.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        foreach (var kvp in kvps)
        {
            var delimiterIndex = kvp.IndexOf(ValueDelimiter, StringComparison.Ordinal);
            if (delimiterIndex == -1)
            {
                dict.Add(
                    decodeResults ? HttpUtility.UrlDecode(kvp)
                                  : kvp,
                    String.Empty);
            }
            else
            {
                dict.Add(
                    decodeResults ? HttpUtility.UrlDecode(kvp[..delimiterIndex])
                                  : kvp[..delimiterIndex],
                    decodeResults ? HttpUtility.UrlDecode(kvp[(delimiterIndex + 1)..])
                                  : kvp[(delimiterIndex + 1)..]);
            }
        }

        return dict;
    }

    internal static Uri InternalUriCreateWithQueryStringValues(string uriBase, IDictionary<string, string> queryStringValues, string fragment)
    {
        var sb = new StringBuilder(200);
        sb = sb.Append(uriBase);

        if (queryStringValues.Count > 0)
        {
            sb = sb.Append(QueryStringDelimiter);

            foreach (var key in queryStringValues.Keys)
            {
                sb = sb.AppendFormat(CultureInfo.InvariantCulture,
                                     "{0}{1}{2}{3}",
                                     key,
                                     ValueDelimiter[0],
                                     queryStringValues[key],
                                     StatePairDelimiter[0]);
            }

            // Strip off the last delimiter between internal state pairs
            sb = sb.Remove(sb.Length - 1, 1);
        }

        if (!String.IsNullOrEmpty(fragment))
        {
            sb.AppendFormat(CultureInfo.InvariantCulture,
                            "{0}{1}",
                            FragmentDelimiter,
                            fragment);
        }

        return new Uri(sb.ToString(), UriKind.Relative);
    }

    #endregion Methods acting on internal Uris

    #endregion Methods
}
