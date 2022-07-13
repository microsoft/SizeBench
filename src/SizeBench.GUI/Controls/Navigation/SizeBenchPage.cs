using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SizeBench.GUI.Core;
using SizeBench.GUI.Navigation;

namespace SizeBench.GUI.Controls.Navigation;

// This type is excluded from code coverage because it's basically a big copy/paste from the Silverlight SDK and in Silverlight
// this was extensively tested.  See details at the top of the file for SizeBenchFrame.
[ExcludeFromCodeCoverage]
public class SizeBenchPage : UserControl
{
    #region Constructors

    public SizeBenchPage()
    {
        DataContextChanged += SizeBenchPage_DataContextChanged;
        this.Background = Brushes.White;
    }

    private void SizeBenchPage_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ViewModelBase oldViewModelBase)
        {
            oldViewModelBase.RequestNavigateToModel -= ViewModelBase_RequestNavigateToModel;
            oldViewModelBase.RequestFragmentNavigation -= ViewModelBase_RequestFragmentNavigation;
        }
        if (e.NewValue is ViewModelBase newViewModelBase)
        {
            newViewModelBase.RequestNavigateToModel += ViewModelBase_RequestNavigateToModel;
            newViewModelBase.RequestFragmentNavigation += ViewModelBase_RequestFragmentNavigation;
        }
    }

    private void ViewModelBase_RequestNavigateToModel(object? sender, object model)
    {
        if (this.DataContext is BinaryDiffViewModelBase diffViewModelBase)
        {
            var newUri = DiffModelToUriConverter.ModelToUri(model, diffViewModelBase.DiffSession);
            this.NavigationService.Navigate(newUri);
        }
        else if (this.DataContext is SingleBinaryViewModelBase)
        {
            var newUri = SingleBinaryModelToUriConverter.ModelToUri(model);
            this.NavigationService.Navigate(newUri);
        }
    }

    private void ViewModelBase_RequestFragmentNavigation(object? sender, string newFragment)
        => this.NavigationService.Navigate(new Uri("#" + newFragment, UriKind.Relative));

    #endregion Constructors

    #region Properties

    public NavigationContext NavigationContext => SizeBenchJournalEntry.GetNavigationContext(this);

    public SizeBenchNavigationService NavigationService => SizeBenchNavigationService.GetSizeBenchNavigationService(this);

    // Having our own DependencyProperty ensures that Title can be data-bound to, but we won't store the data
    // we'll just forward it to JournalEntry.Name
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register("Title",
                                    typeof(string),
                                    typeof(SizeBenchPage),
                                    new PropertyMetadata(OnTitleChanged));

    private static void OnTitleChanged(DependencyObject depObj, DependencyPropertyChangedEventArgs args)
    {
        var that = (SizeBenchPage)depObj;
        SizeBenchJournalEntry.SetName(that, (string)args.NewValue);
    }

    /// <summary>
    /// Gets or sets the name for the page to display to the user in the navigation history.
    /// </summary>
    public string Title
    {
        get => SizeBenchJournalEntry.GetName(this);
        set => SizeBenchJournalEntry.SetName(this, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether this page should never be cached, should be cached
    /// for an indefinite length of time, or should only be cached within the size limitations of
    /// the cache.
    /// </summary>
    public NavigationCacheMode NavigationCacheMode
    {
        get => NavigationCache.GetNavigationCacheMode(this);
        set => NavigationCache.SetNavigationCacheMode(this, value);
    }

    #endregion Properties

    #region Methods

    internal void InternalOnViewModelReady()
        => OnViewModelReady();

    internal void InternalOnFragmentNavigation(SizeBenchFragmentNavigationEventArgs e)
        => OnFragmentNavigation(e);

    internal void InternalOnNavigatedTo(SizeBenchNavigationEventArgs e) => OnNavigatedTo(e);

    internal void InternalOnNavigatingFrom(SizeBenchNavigatingCancelEventArgs e)
        => OnNavigatingFrom(e);

    internal void InternalOnNavigatedFrom(SizeBenchNavigationEventArgs e)
    {
        OnNavigatedFrom(e);
        if (this.DataContext is ViewModelBase viewModelBase &&
            this.NavigationCacheMode == NavigationCacheMode.Disabled)
        {
            viewModelBase.Deactivate();
        }
    }

    protected virtual void OnViewModelReady()
    {
        return;
    }

    /// <summary>
    /// This method is called when fragment navigation occurs on a page - either because a fragment
    /// was present in the original Uri that navigated to this page, or because a later fragment
    /// navigation occurs.
    /// </summary>
    /// <remarks>
    /// This should be used rather than signing up for NavigationService.FragmentNavigation
    /// because that event may be difficult to sign up for in time to get the first fragment navigation.
    /// </remarks>
    /// <param name="e">The event arguments, containing the fragment navigated to</param>
    protected virtual void OnFragmentNavigation(SizeBenchFragmentNavigationEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (this.DataContext is ViewModelBase viewModelBase)
        {
            viewModelBase.SetCurrentFragment(e.Fragment);
        }

        return;
    }

    /// <summary>
    /// This method is called when a Page has been navigated to, and becomes the active Page in a Frame.
    /// This method is the preferred place to inspect NavigationContext, and react to load-time
    /// information and prepare the page for viewing.
    /// </summary>
    /// <remarks>
    /// This should be used rather than Loaded because Loaded signifies you have been added to the visual
    /// tree, but that could potentially happen more than once during a logical navigation event, in
    /// some advanced scenarios.  This method is guaranteed to be called only once when the Page becomes
    /// active.
    /// </remarks>
    /// <param name="e">The event arguments</param>
    protected virtual void OnNavigatedTo(SizeBenchNavigationEventArgs e)
    {
        return;
    }

    /// <summary>
    /// This method is called when a Page is about to be navigated away from.
    /// </summary>
    /// <remarks>
    /// This is similar to signing up for NavigationService.Navigating, but this method is preferred
    /// as then you do not need to remove the event handler from NavigationService to avoid object lifetime
    /// issues.
    /// </remarks>
    /// <param name="e">The event arguments.  If Cancel is set to true, it will cancel the pending operation that triggered this method call.</param>
    protected virtual void OnNavigatingFrom(SizeBenchNavigatingCancelEventArgs e)
    {
        return;
    }

    /// <summary>
    /// This method is called when a Page has been navigated away from, and is no longer the active
    /// Page in a Frame.  This is a good time to save dirty data or otherwise react to being
    /// inactive.
    /// </summary>
    /// <remarks>
    /// This is similar to signing up for NavigationService.Navigated, but this method is preferred
    /// as then you do not need to remove the event handler from NavigationService to avoid object lifetime
    /// issues.
    /// </remarks>
    /// <param name="e">The event arguments</param>
    protected virtual void OnNavigatedFrom(SizeBenchNavigationEventArgs e)
    {
        return;
    }

    #endregion Methods
}
