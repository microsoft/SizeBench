using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace SizeBench.GUI.Controls.Navigation;

// This type is excluded from code coverage because it's basically a big copy/paste from the Silverlight SDK and in Silverlight
// this was extensively tested.  See details at the top of the file for SizeBenchFrame.
[ExcludeFromCodeCoverage]
public sealed class UriMapping
{
    #region Fields

    private static readonly Regex _conversionRegex = new Regex("(?<ConversionCapture>{.*?})", RegexOptions.ExplicitCapture);
    private Uri? _uri;
    private Uri? _mappedUri;
    private Regex? _uriRegex;
    private bool _uriRegexIdentifierUsedTwice;
    private bool _uriHasQueryString;
    private bool _uriHasFragment;
    private bool _mappedUriIsOnlyFragment;
    private bool _mappedUriIsOnlyQueryString;
    private readonly List<string> _uriIdentifiers = new List<string>();
    private readonly List<string> _mappedUriIdentifiers = new List<string>();
    private bool _initialized;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets or sets the original URI value or pattern.
    /// </summary>
    public Uri? Uri
    {
        get => this._uri;

        set
        {
            this._uri = value;
            this._initialized = false;
        }
    }

    /// <summary>
    /// Gets or sets the mapped URI value or pattern.
    /// </summary>
    public Uri? MappedUri
    {
        get => this._mappedUri;

        set
        {
            this._mappedUri = value;
            this._initialized = false;
        }
    }

    #endregion Properties

    #region Methods

    private bool UriTemplateContainsSameIdentifierTwice(Uri? uri, out Regex? uriRegex)
    {
        if (uri is null)
        {
            uriRegex = null;
            return false;
        }

        var origString = uri.OriginalString;
        var matches = _conversionRegex.Matches(origString);
        this._uriIdentifiers.Clear();

        foreach (Match? m in matches)
        {
            var valWithoutBraces = m!.Value.Replace("{", String.Empty, StringComparison.Ordinal).Replace("}", String.Empty, StringComparison.Ordinal);

            // We've hit the same identifier being used twice.  This isn't valid.
            if (this._uriIdentifiers.Contains(valWithoutBraces))
            {
                uriRegex = null;
                return true;
            }

            this._uriIdentifiers.Add(valWithoutBraces);
        }

        var convertedValue = _conversionRegex.Replace(origString, "(?<$1>.*?)").Replace("{", String.Empty, StringComparison.Ordinal).Replace("}", String.Empty, StringComparison.Ordinal);
        uriRegex = new Regex("^" + convertedValue + "$");
        return false;
    }

    private void GetIdentifiersForMappedUri(Uri mappedUri)
    {
        var origString = mappedUri.OriginalString;
        var matches = _conversionRegex.Matches(origString);
        this._mappedUriIdentifiers.Clear();

        foreach (Match? m in matches)
        {
            var valWithoutBraces = m!.Value.Replace("{", String.Empty, StringComparison.Ordinal).Replace("}", String.Empty, StringComparison.Ordinal);
            if (!this._mappedUriIdentifiers.Contains(valWithoutBraces))
            {
                this._mappedUriIdentifiers.Add(valWithoutBraces);
            }
        }
    }

    private void Initialize()
    {
        // Initialize stuff for the Uri template
        this._uriRegexIdentifierUsedTwice = UriTemplateContainsSameIdentifierTwice(this._uri, out var newFromRegex);
        this._uriHasQueryString = !String.IsNullOrEmpty(UriParsingHelper.InternalUriGetQueryString(this._uri));
        this._uriHasFragment = !String.IsNullOrEmpty(UriParsingHelper.InternalUriGetFragment(this._uri));
        this._uriRegex = newFromRegex;
        this._mappedUriIsOnlyFragment = UriParsingHelper.InternalUriIsFragment(this._mappedUri);
        // It's safe to deref this._mappedUri on the line below, because this code is only called if this._mappedUri != null (from CheckPreconditions)
        this._mappedUriIsOnlyQueryString = UriParsingHelper.QueryStringDelimiter + UriParsingHelper.InternalUriGetQueryString(this._mappedUri) == this._mappedUri!.OriginalString;

        // Initialize stuff for the mapped Uri template
        GetIdentifiersForMappedUri(this._mappedUri);

        this._initialized = true;
    }

    /// <summary>
    /// Attempts to process a Uri, if it matches the Uri template
    /// </summary>
    /// <param name="uri">The Uri to map</param>
    /// <returns>The Uri after mapping, or null if mapping did not succeed</returns>
    public Uri? MapUri(Uri? uri)
    {
        CheckPreconditions();

        if (this._uriRegex is null)
        {
            // If an empty Uri was passed in, we can map that even with an empty Uri Template.
            if (uri is null || uri.OriginalString is null || uri.OriginalString.Length == 0)
            {
                // this._mappedUri is safe to deref here since CheckPreconditions above will throw if it's null.
                return new Uri(this._mappedUri!.OriginalString, UriKind.Relative);
            }
            // Otherwise, this does not match anything
            else
            {
                return null;
            }
        }

        if (uri is null)
        {
            return null;
        }

        var originalUriWithoutQueryString = UriParsingHelper.InternalUriGetBaseValue(uri);

        var m = this._uriRegex.Match(originalUriWithoutQueryString);

        if (!m.Success)
        {
            return null;
        }

        // this._mappedUri is safe to deref on the next line, since it's guaranteed to be non-null by CheckPreconditions above.
        var uriAfterMappingBase = UriParsingHelper.InternalUriGetBaseValue(this._mappedUri!);
        var uriAfterMappingQueryString = UriParsingHelper.InternalUriParseQueryStringToDictionary(this._mappedUri, false /* decodeResults */);
        var originalQueryString = UriParsingHelper.InternalUriParseQueryStringToDictionary(uri, false /* decodeResults */);
        var originalFragment = UriParsingHelper.InternalUriGetFragment(uri);
        var uriAfterMappingFragment = UriParsingHelper.InternalUriGetFragment(this._mappedUri);

        // 'uriValues' is the values of the identifiers from the 'Uri' template, as they appear in the Uri
        // being processed
        var uriValues = new Dictionary<string, string>();

        // i begins at 1 because the group at index 0 is always equal to the parent's Match,
        // which we do not want.  We only want explicitly-named groups.
        var groupCount = m.Groups.Count;
        for (var i = 1; i < groupCount; i++)
        {
            uriValues.Add(this._uriRegex.GroupNameFromNumber(i), m.Groups[i].Value);
        }

        foreach (var identifier in this._mappedUriIdentifiers)
        {
            var identifierWithBraces = "{" + identifier + "}";
            var replacementValue = (uriValues.TryGetValue(identifier, out var value) ? value : String.Empty);

            // First check for identifiers in the base Uri, and replace them as appropriate
            uriAfterMappingBase = uriAfterMappingBase.Replace(identifierWithBraces, replacementValue, StringComparison.Ordinal);

            // Then, look through the query string (both the key and the value) and replace as appropriate
            var keys = new string[uriAfterMappingQueryString.Keys.Count];
            uriAfterMappingQueryString.Keys.CopyTo(keys, 0);
            foreach (var key in keys)
            {
                // First check if the value contains it, as this is an easy replacement
                if (uriAfterMappingQueryString[key].Contains(identifierWithBraces, StringComparison.Ordinal))
                {
                    if (uriValues.ContainsKey(identifier))
                    {
                        uriAfterMappingQueryString[key] = uriAfterMappingQueryString[key].Replace(identifierWithBraces, replacementValue, StringComparison.Ordinal);
                    }
                }

                // If the key itself contains the identifier, then we need to remove the existing item with the key that
                // contains the identifier, and re-add to the dictionary with the new key and the pre-existing value
                if (key.Contains(identifierWithBraces, StringComparison.Ordinal))
                {
                    var existingVal = uriAfterMappingQueryString[key];
                    uriAfterMappingQueryString.Remove(key);
                    uriAfterMappingQueryString.Add(key.Replace(identifierWithBraces, replacementValue, StringComparison.Ordinal), existingVal);
                }
            }

            // If there's an original fragment already present, it will always win, so don't bother doing replacements
            if (String.IsNullOrEmpty(originalFragment) &&
                !String.IsNullOrEmpty(uriAfterMappingFragment))
            {
                if (uriAfterMappingFragment.Contains(identifierWithBraces, StringComparison.Ordinal))
                {
                    uriAfterMappingFragment = uriAfterMappingFragment.Replace(identifierWithBraces, replacementValue, StringComparison.Ordinal);
                }
            }
        }

        foreach (var key in originalQueryString.Keys)
        {
            if (!uriAfterMappingQueryString.ContainsKey(key))
            {
                uriAfterMappingQueryString.Add(key, originalQueryString[key]);
            }
            else
            {
                // If a value is present in the originally-navigated-to query string, it
                // takes precedence over anything in the aliased query string by default.
                uriAfterMappingQueryString[key] = originalQueryString[key];
            }
        }

        if (!String.IsNullOrEmpty(originalFragment))
        {
            uriAfterMappingFragment = originalFragment;
        }

        return UriParsingHelper.InternalUriCreateWithQueryStringValues(uriAfterMappingBase, uriAfterMappingQueryString, uriAfterMappingFragment);
    }

    private void CheckPreconditions()
    {
        if (this._mappedUri is null)
        {
            throw new InvalidOperationException("MappedUri template must be specified.");
        }

        if (this._initialized == false)
        {
            Initialize();
        }

        if (this._uriHasQueryString)
        {
            throw new InvalidOperationException("Uri template cannot have a query string.");
        }

        if (this._uriHasFragment)
        {
            throw new InvalidOperationException("Uri template cannot have a fragment.");
        }

        if (this._uriRegexIdentifierUsedTwice)
        {
            throw new InvalidOperationException("Uri template cannot contain the same identifier more than once.");
        }

        if (this._mappedUriIsOnlyFragment)
        {
            throw new InvalidOperationException("MappedUri cannot be a URI fragment (cannot begin with '#').");
        }

        if (this._mappedUriIsOnlyQueryString)
        {
            throw new InvalidOperationException("MappedUri cannot be only a query string.");
        }
    }

    #endregion Methods
}
