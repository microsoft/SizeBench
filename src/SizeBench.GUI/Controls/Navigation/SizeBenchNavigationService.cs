using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Controls.Navigation;

// This type is excluded from code coverage because it's basically a big copy/paste from the Silverlight SDK and in Silverlight
// this was extensively tested.  See details at the top of the file for SizeBenchFrame.
[ExcludeFromCodeCoverage]
public sealed class SizeBenchNavigationService
{
    #region Fields

    private bool _journalIsAddingHistoryPoint;
    private Uri? _currentSourceAfterMapping;
    private Uri? _source;
    private NavigationOperation? _currentNavigation;
    private readonly Dictionary<string, SizeBenchPage> _cacheRequiredPages = new Dictionary<string, SizeBenchPage>();

    #endregion Fields

    #region Constructors

    internal SizeBenchNavigationService(SizeBenchFrame nav)
    {
        ArgumentNullException.ThrowIfNull(nav);
        this.Host = nav;
    }

    #endregion Constructors

    #region Events

    public event EventHandler<SizeBenchNavigationFailedEventArgs>? NavigationFailed;

    public event EventHandler<SizeBenchNavigatingCancelEventArgs>? Navigating;

    public event EventHandler<SizeBenchNavigationEventArgs>? Navigated;

    public event EventHandler<SizeBenchNavigationEventArgs>? NavigationStopped;

    public event EventHandler<SizeBenchFragmentNavigationEventArgs>? FragmentNavigation;

    #endregion Events

    #region SizeBenchNavigationService Attached Property


    internal static readonly DependencyProperty SizeBenchNavigationServiceProperty = DependencyProperty.RegisterAttached(
                    "SizeBenchNavigationService",
                    typeof(SizeBenchNavigationService),
                    typeof(SizeBenchNavigationService),
                    new PropertyMetadata(null));

    internal static SizeBenchNavigationService GetSizeBenchNavigationService(DependencyObject dependencyObject)
    {
        ArgumentNullException.ThrowIfNull(dependencyObject);

        return (SizeBenchNavigationService)dependencyObject.GetValue(SizeBenchNavigationServiceProperty);
    }

    #endregion SizeBenchNavigationService Attached Property

    #region Properties

    internal SizeBenchJournal Journal { get; private set; } = new SizeBenchJournal();

    internal INavigationContentLoader ContentLoader
    {
        get;
        set;
    } = new PageResourceContentLoader();

    internal SizeBenchFrame Host { get; }

    internal bool IsNavigating => this._currentNavigation != null;

    internal NavigationCache Cache
    {
        get;
        private set;
    } = new NavigationCache(initialCacheSize: 5);

    public Uri? Source
    {
        get => this._source;
        set
        {
            this._source = value;
            Navigate(value);
        }
    }

    public Uri? CurrentSource { get; internal set; }

    public bool CanGoForward => this.Journal.CanGoForward;

    public bool CanGoBack => this.Journal.CanGoBack;

    #endregion Properties

    #region Methods

    internal void InitializeJournal()
    {
        this.Journal.Navigated -= Journal_Navigated;
        this.Journal = new SizeBenchJournal();
        this.Journal.Navigated += Journal_Navigated;
    }

    internal void InitializeNavigationCache()
        => this.Cache = new NavigationCache(this.Host.CacheSize);

    public Task<bool> Navigate(Uri? source)
        => NavigateCore(source, NavigationMode.New, false/*suppressJournalAdd*/, false/*isRedirect*/);

    private void Journal_Navigated(object? _, JournalEventArgs args)
    {
        if (this._journalIsAddingHistoryPoint == false)
        {
            var navOp = this._currentNavigation;
            if (navOp is null || navOp.SuppressNotifications == false)
            {
                NavigateCore(args.Uri, args.NavigationMode, true/*suppressJournalAdd*/, false/*isRedirect*/);
            }
        }
    }

    private Task<bool> NavigateCore(Uri? uri, NavigationMode mode, bool suppressJournalAdd, bool isRedirect)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(uri);

            // Make sure we're on the UI thread because of the DependencyProperties we use.
            if (!this.Host.Dispatcher.CheckAccess())
            {
                // Move to UI thread
                this.Host.Dispatcher.BeginInvoke(new Action(() => NavigateCore(uri, mode, suppressJournalAdd, isRedirect)));
                return Task.FromResult(true);
            }

            var mappedUri = uri;
            // If the Uri is only a fragment, mapping does not take place
            if (!UriParsingHelper.InternalUriIsFragment(uri))
            {
                var mapper = this.Host.UriMapper;
                if (mapper != null)
                {
                    var uriFromMapper = mapper.MapUri(uri);
                    if (uriFromMapper != null && !String.IsNullOrEmpty(uriFromMapper.OriginalString))
                    {
                        mappedUri = uriFromMapper;
                    }
                    else
                    {
                        mappedUri = uri;
                    }
                }
            }

            var mergedUriAfterMapping = UriParsingHelper.InternalUriMerge(this._currentSourceAfterMapping, mappedUri) ?? mappedUri;
            var mergedUri = UriParsingHelper.InternalUriMerge(this.CurrentSource, uri) ?? uri;

            // If we're navigating to just a fragment (i.e. "#frag1") or to a page which differs only in the fragment
            // (i.e. "Page.xaml?id=123" to "Page.xaml?id=123#frag1") then complete navigation without involving the content loader
            var isFragmentNavigationOnly = (mode != NavigationMode.Refresh) &&
                                           (UriParsingHelper.InternalUriIsFragment(mappedUri) ||
                                            UriParsingHelper.InternalUriGetAllButFragment(mergedUri) == UriParsingHelper.InternalUriGetAllButFragment(this.CurrentSource));

            if (isFragmentNavigationOnly && UriParsingHelper.InternalUriGetFragment(this.CurrentSource) == UriParsingHelper.InternalUriGetFragment(mappedUri))
            {
                // We're navigating to the same fragment as we're already on - nothing to do, bail.
                return Task.FromResult(true);
            }

            // Check to see if anyone wants to cancel
            if (mode is NavigationMode.New or NavigationMode.Refresh)
            {
                if (RaiseNavigating(mergedUri, mode, isFragmentNavigationOnly) == true)
                {
                    // Someone stopped us
                    RaiseNavigationStopped(null, mergedUri);
                    return Task.FromResult(true);
                }
            }

            // If the ContentLoader cannot load the new URI, throw an ArgumentException
            if (!this.ContentLoader.CanLoad(mappedUri, this._currentSourceAfterMapping))
            {
                throw new ArgumentException("Content for the URI cannot be loaded. The URI may be invalid.", nameof(uri));
            }

            if (isFragmentNavigationOnly && this.Host.Content is null)
            {
                // It doesn't make sense to fragment navigate when there's no content, so raise NavigationFailed
                throw new InvalidOperationException(String.Format(CultureInfo.InvariantCulture,
                                                                  "Navigation to a fragment requires the {0} to have content currently.",
                                                                  "Frame"));
            }

            if (isRedirect && this._currentNavigation != null &&
                this._currentNavigation.UriForJournal == this.CurrentSource)
            {
                // Do not record navigation in the journal in case of a redirection
                // where the original target is the current URI.
                suppressJournalAdd = true;
            }

            // Stop in-progress navigation
            StopLoadingCore();

            return NavigateCore_StartNavigation(uri, mode, suppressJournalAdd, mergedUriAfterMapping, mergedUri, isFragmentNavigationOnly);
        }
        catch (Exception ex)
        {
            if (RaiseNavigationFailed(uri, ex))
            {
                throw;
            }
            return Task.FromResult(true);
        }
    }

    private async Task<bool> NavigateCore_StartNavigation(Uri uri, NavigationMode mode, bool suppressJournalAdd, Uri mergedUriAfterMapping, Uri mergedUri, bool isFragmentNavigationOnly)
    {
        this._currentNavigation = new NavigationOperation(mergedUriAfterMapping, mergedUri, uri, mode, suppressJournalAdd);

        if (isFragmentNavigationOnly)
        {
            // If we're navigating only to a fragment (e.g. "#frag2") then the Uri to journal should be that merged with the base uri
            if (UriParsingHelper.InternalUriIsFragment(uri))
            {
                this._currentNavigation.UriForJournal = mergedUri;
            }
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed - here that is desirable, we intend to post this to the queue for later
            this.Host.Dispatcher.BeginInvoke(new Action(() => CompleteNavigation(null)));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            return true;
        }

        UpdateNavigationCacheModeAlwaysPages();

        var uriAllButFragment = UriParsingHelper.InternalUriGetAllButFragment(uri);

        SizeBenchPage? reusedPage = null;

        if (this._cacheRequiredPages.ContainsKey(uriAllButFragment))
        {
            reusedPage = this._cacheRequiredPages[uriAllButFragment];
        }
        else if (this.Cache.Contains(uriAllButFragment))
        {
            reusedPage = this.Cache[uriAllButFragment];
        }

        // If a page was found in either cache and that page hasn't yet changed its NavigationCacheMode to Disabled,
        // then navigation is done, otherwise open up new content
        if (reusedPage != null && reusedPage.NavigationCacheMode != NavigationCacheMode.Disabled)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed - here that is desirable, we intend to post this to the queue for later
            this.Host.Dispatcher.BeginInvoke(new Action(() => CompleteNavigation(reusedPage)));
#pragma warning restore CS4014
            return true;
        }

        var loadResult = await this.ContentLoader.LoadContentAsync(mergedUriAfterMapping, this._currentSourceAfterMapping);

        try
        {
            if (loadResult is null)
            {
                throw new InvalidOperationException(String.Format(CultureInfo.InvariantCulture, "Invalid LoadResult returned by the {0}.", this.ContentLoader.GetType()));
            }
            else
            {
                if (loadResult.LoadedContent is not SizeBenchPage content)
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.InvariantCulture, $"The content loaded was of type {loadResult.LoadedContent.GetType()}, which is not a subclass of SizeBench.GUI.Controls.Navigation.SizeBenchPage."));
                }

                // Content loader was successful, so complete navigation

                var viewModelType = Type.GetType(content.GetType().FullName + "ViewModel");
                var viewModel = (ViewModelBase)this.Host.WindsorContainer.Resolve(viewModelType);

                // Create a new navigation context
                var navContext = new NavigationContext(UriParsingHelper.InternalUriParseQueryStringToDictionary(mergedUriAfterMapping, true /* decodeResults */));
                SizeBenchJournalEntry.SetNavigationContext(content, navContext);
                content.SetValue(SizeBenchNavigationServiceProperty, this);

                viewModel.SetQueryString(navContext.QueryString);
                await viewModel.SetCurrentFragment(UriParsingHelper.InternalUriGetFragment(mergedUri));
                await viewModel.InitializeAsync();
                content.DataContext = viewModel;
                content.InternalOnViewModelReady();

                // Complete navigation operation
                CompleteNavigation(content);
            }
        }
        catch (Exception ex)
        {
            if (RaiseNavigationFailed(mergedUri, ex))
            {
                throw;
            }
            return false;
        }

        return true;
    }

    private void CompleteNavigation(SizeBenchPage? newContentPage)
    {
        Uri? uri = null;
        var existingContentPage = (SizeBenchPage)this.Host.Content;

        var pageTitle = SizeBenchJournalEntry.GetName(newContentPage ?? existingContentPage);

        var navOp = this._currentNavigation;
        this._currentNavigation = null;
        if (navOp != null)
        {
            // Set uri
            uri = navOp.UriBeforeMapping;

            // Used to suppress navigation notifications.
            navOp.SuppressNotifications = true;

            if (this.CurrentSource == navOp.UriForJournal)
            {
                // Do not record the navigation in the journal when moving to the same URI whether this
                // is a redirection or not.
                navOp.SuppressJournalAdd = true;
            }
            this.CurrentSource = navOp.UriForJournal;
            this._source = navOp.UriBeforeMapping;
            this._currentSourceAfterMapping = navOp.Uri;
            this.Host.UpdateSourceFromNavigationService(navOp.UriForJournal);
            this.Host.CurrentSource = this.CurrentSource;
            if (newContentPage != null)
            {
                var b = new Binding()
                {
                    Source = newContentPage,
                    Path = new PropertyPath("Title")
                };
                this.Host.SetBinding(SizeBenchFrame.InternalCurrentPageTitleProperty, b);
            }

            // Check if this is a 'New' operation
            if (navOp.Mode == NavigationMode.New && navOp.Uri != null && navOp.SuppressJournalAdd == false)
            {
                try
                {
                    this._journalIsAddingHistoryPoint = true;
                    var je = new SizeBenchJournalEntry(pageTitle ?? uri.OriginalString, navOp.UriForJournal);
                    this.Journal.AddHistoryPoint(je);
                }
                finally
                {
                    this._journalIsAddingHistoryPoint = false;
                }
            }

            this.Host.CanGoBack = this.CanGoBack;
            this.Host.CanGoForward = this.CanGoForward;

            navOp.SuppressNotifications = false;
        }

        if (newContentPage is null)
        {
            // We're navigating to a fragment in the current page, so for WPF compatibility, fire FragmentNavigation THEN Navigated
            if (navOp != null)
            {
                RaiseFragmentNavigation(UriParsingHelper.InternalUriGetFragment(navOp.Uri));
                RaiseNavigated(newContentPage, uri, existingContentPage);
            }
        }
        else
        {
            // We're navigating to a fragment in the new content, so let the host load content, then for WPF compatibility,
            // fire Navigated THEN FragmentNavigation
            this.Host.Content = newContentPage;
            RaiseNavigated(newContentPage, uri, existingContentPage);
            var fragment = navOp is null ? null : UriParsingHelper.InternalUriGetFragment(navOp.Uri);
            if (!String.IsNullOrEmpty(fragment))
            {
                RaiseFragmentNavigation(fragment);
            }
        }
    }

    private void UpdateNavigationCacheModeAlwaysPages()
    {
        if (this.Host.Content is SizeBenchPage currentPage)
        {
            var currentSourceWithoutFragment = UriParsingHelper.InternalUriGetAllButFragment(this.CurrentSource);

            if (currentPage.NavigationCacheMode == NavigationCacheMode.Required)
            {
                // If this page is NavigationCacheMode == "Required" then put it in the dictionary to store a hard reference
                // to it so it can be re-used by future navigations.
                this._cacheRequiredPages[currentSourceWithoutFragment] = currentPage;
            }
            else
            {
                // We must always try to remove, just in case this page used to be Required and is now Enabled or Disabled
                this._cacheRequiredPages.Remove(currentSourceWithoutFragment);
            }

            if (currentPage.NavigationCacheMode == NavigationCacheMode.Enabled)
            {
                // If this page is NavigationCacheMode == "Enabled" then put it in the cache
                this.Cache.AddToCache(currentSourceWithoutFragment, currentPage);
            }
            else
            {
                // We must always try to remove in case it went from Enabled to Disabled or Required
                this.Cache.RemoveFromCache(currentSourceWithoutFragment);
            }
        }
    }

    public void GoForward()
    {
        GoForwardBackCore(NavigationMode.Forward,
                               this.CanGoForward,
                               this.Journal.ForwardStack);
    }

    public void GoBack()
    {
        GoForwardBackCore(NavigationMode.Back,
                               this.CanGoBack,
                               this.Journal.BackStack);
    }

    public void Refresh()
        => NavigateCore(this.CurrentSource, NavigationMode.Refresh, true/*suppressJournalAdd*/, false/*isRedirect*/);

    /// <summary>
    /// StopLoading aborts asynchronous navigations that haven't been processed yet.
    /// The <see cref="NavigationStopped"/> event is raised only if the navigation was actually aborted - if navigation is
    /// too far along to be canceled, then navigation may still complete and the <see cref="Navigated"/> event
    /// will be raised.
    /// </summary>
    public void StopLoading() => StopLoadingCore();

    private void StopLoadingCore()
    {
        var navOp = this._currentNavigation;
        if (navOp != null)
        {
            RaiseNavigationStopped(null, navOp.Uri);

            // Release current context
            this._currentNavigation = null;
        }
    }

    private void GoForwardBackCore(NavigationMode mode, bool canDoIt, Stack<SizeBenchJournalEntry> entries)
    {
        if (canDoIt)
        {
            var entry = entries.Peek();

            var isFragmentNavigationOnly =
                UriParsingHelper.InternalUriIsFragment(entry.Source) ||
                UriParsingHelper.InternalUriGetAllButFragment(entry.Source) == UriParsingHelper.InternalUriGetAllButFragment(this._currentSourceAfterMapping);

            if (RaiseNavigating(entry.Source, mode, isFragmentNavigationOnly) == false)
            {
                if (mode == NavigationMode.Back)
                {
                    this.Journal.GoBack();
                }
                else
                {
                    this.Journal.GoForward();
                }
            }
            else
            {
                RaiseNavigationStopped(null, entry.Source);
            }
        }
    }

    #region Event handlers

    private void RaiseNavigated(SizeBenchPage? content, Uri? uri, SizeBenchPage? existingContentPage)
    {
        var eventHandler = Navigated;

        if (eventHandler != null)
        {
            var eventArgs = new SizeBenchNavigationEventArgs(content, uri);
            eventHandler(this, eventArgs);
        }

        if (existingContentPage != null && content != null)
        {
            existingContentPage.InternalOnNavigatedFrom(new SizeBenchNavigationEventArgs(content, uri));
        }

        content?.InternalOnNavigatedTo(new SizeBenchNavigationEventArgs(content, uri));
    }

    private bool RaiseNavigating(Uri uri, NavigationMode mode, bool isFragmentNavigationOnly)
    {
        var eventHandler = Navigating;
        var canceled = false;

        if (eventHandler != null)
        {
            var eventArgs = new SizeBenchNavigatingCancelEventArgs(uri, mode);

            eventHandler(this, eventArgs);

            canceled = eventArgs.Cancel;
        }

        if (!isFragmentNavigationOnly)
        {
            if (this.Host.Content is SizeBenchPage p)
            {
                var eventArgs = new SizeBenchNavigatingCancelEventArgs(uri, mode);
                p.InternalOnNavigatingFrom(eventArgs);
                canceled |= eventArgs.Cancel;
            }
        }

        return canceled;
    }

    private bool RaiseNavigationFailed(Uri? uri, Exception exception)
    {
        var eventArgs = new SizeBenchNavigationFailedEventArgs(uri, exception);

        NavigationFailed?.Invoke(this, eventArgs);

        return !eventArgs.Handled;
    }

    private void RaiseNavigationStopped(object? content, Uri uri)
    {
        var eventArgs = new SizeBenchNavigationEventArgs(content, uri);

        NavigationStopped?.Invoke(this, eventArgs);
    }

    private void RaiseFragmentNavigation(string fragment)
    {
        var eventArgs = new SizeBenchFragmentNavigationEventArgs(fragment);
        FragmentNavigation?.Invoke(this, eventArgs);

        if (this.Host.Content is SizeBenchPage p)
        {
            p.InternalOnFragmentNavigation(eventArgs);
        }
    }

    #endregion Event handlers

    #endregion Methods

    #region Nested Classes, Structs

    /// <summary>
    /// Class used within the Frame to manage navigation operations.
    /// </summary>
    [ExcludeFromCodeCoverage]
    private class NavigationOperation
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="uri">The Uri after it was mapped</param>
        /// <param name="uriBeforeMapping">The Uri before it was mapped</param>
        /// <param name="uriForJournal">The Uri to use for the journal</param>
        /// <param name="mode">The mode (new, forward, or back) of this operation</param>
        /// <param name="suppressJournalUpdate">True if the journal shouldn't be updated by this operation, false otherwise</param>
        public NavigationOperation(Uri uri, Uri uriBeforeMapping, Uri uriForJournal, NavigationMode mode, bool suppressJournalUpdate)
        {
            this.Uri = uri;
            this.UriBeforeMapping = uriBeforeMapping;
            this.UriForJournal = uriForJournal;
            this.Mode = mode;
            this.SuppressJournalAdd = suppressJournalUpdate;
        }

        /// <summary>
        /// Gets or sets Uri used in the navigation operation, after passing through the UriMapper
        /// </summary>
        public Uri Uri
        {
            get;
            set;
        }

        public Uri UriBeforeMapping
        {
            get;
            set;
        }

        public Uri UriForJournal { get; set; }

        /// <summary>
        /// Gets or sets NavigationMode used in the current operation.
        /// </summary>
        public NavigationMode Mode
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not the operation is altering the Source property.
        /// </summary>
        public bool SuppressNotifications
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the Journal should be updated based on this navigation operation
        /// </summary>
        public bool SuppressJournalAdd
        {
            get;
            set;
        }
    }

    #endregion Nested Classes, Structs
}
