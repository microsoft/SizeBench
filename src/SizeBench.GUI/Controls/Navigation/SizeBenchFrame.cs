using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Castle.Windsor;

namespace SizeBench.GUI.Controls.Navigation;

// WPF's Frame control is terribly frustrating.  It has built-in caching support that can't be disabled but which doesn't
// work well with SizeBench's desires.  It does weird things with URI manipulation and has bugs.  It does a lot of funky
// stuff to try to vaguely feel like a browser, even though it's bad at being a browser (we can use CEF for that if needed).
//
// Thus, enter SizeBenchFrame.  It's liberally copied from the Silverlight Toolkit, from the implementation I wrote there many
// years ago.
//
// This type is excluded from code coverage because it's basically a big copy/paste from the Silverlight SDK and in Silverlight
// this was extensively tested.  Porting all the tests would be a pain in the butt, so just skipping that step and excluding this
// from code coverage.
[ExcludeFromCodeCoverage]
[TemplatePart(Name = PART_FrameNextButton, Type = typeof(ButtonBase))]
[TemplatePart(Name = PART_FramePreviousButton, Type = typeof(ButtonBase))]
public class SizeBenchFrame : ContentControl
{
    #region Static Fields and Constants

    private const string PART_FrameNextButton = "NextButton";
    private const string PART_FramePreviousButton = "PrevButton";
    private const int DefaultCacheSize = 10;

    #endregion

    #region Fields

    private ButtonBase? _nextButton;
    private ButtonBase? _previousButton;
    private bool _loaded;
    private bool _updatingSourceFromNavigationService;
    private Uri? _deferredNavigation;

    #endregion  Fields

    #region Constructors

    public SizeBenchFrame()
    {
        this.DefaultStyleKey = typeof(SizeBenchFrame);
        Loaded += new RoutedEventHandler(Frame_Loaded);
        this.NavigationService = new SizeBenchNavigationService(this);
        this.CommandBindings.Add(new CommandBinding(NavigationCommands.BrowseBack, OnGoBack, OnQueryGoBack));
        this.CommandBindings.Add(new CommandBinding(NavigationCommands.BrowseForward, OnGoForward, OnQueryGoForward));
        this.CommandBindings.Add(new CommandBinding(NavigationCommands.Refresh, OnRefresh, OnQueryRefresh));
    }

    #endregion Constructors

    #region Events

    public event EventHandler<SizeBenchNavigationEventArgs> Navigated
    {
        add { this.NavigationService.Navigated += value; }
        remove { this.NavigationService.Navigated -= value; }
    }

    public event EventHandler<SizeBenchNavigatingCancelEventArgs> Navigating
    {
        add { this.NavigationService.Navigating += value; }
        remove { this.NavigationService.Navigating -= value; }
    }

    public event EventHandler<SizeBenchNavigationFailedEventArgs> NavigationFailed
    {
        add { this.NavigationService.NavigationFailed += value; }
        remove { this.NavigationService.NavigationFailed -= value; }
    }

    public event EventHandler<SizeBenchNavigationEventArgs> NavigationStopped
    {
        add { this.NavigationService.NavigationStopped += value; }
        remove { this.NavigationService.NavigationStopped -= value; }
    }

    public event EventHandler<SizeBenchFragmentNavigationEventArgs> FragmentNavigation
    {
        add { this.NavigationService.FragmentNavigation += value; }
        remove { this.NavigationService.FragmentNavigation -= value; }
    }

    #endregion Events

    #region Dependency Properties

    #region Source Dependency Property

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(
            "Source",
            typeof(Uri),
            typeof(SizeBenchFrame),
            new PropertyMetadata(SourcePropertyChanged));

    /// <summary>
    /// Gets or sets the uniform resource identifier (URI) of the current
    /// content or the content that is being navigated to.
    /// </summary>
    /// <remarks>
    /// This value may be different from CurrentSource if you set Source and the
    /// navigation has not yet completed.  CurrentSource reflects the page currently
    /// in the frame at all times, even when an async loading operation is in progress.
    /// </remarks>
    public Uri Source
    {
        get => (Uri)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>
    /// Called when Source property is changed
    /// </summary>
    /// <param name="depObj">The dependency property</param>
    /// <param name="e">The event arguments</param>
    private static void SourcePropertyChanged(DependencyObject depObj, DependencyPropertyChangedEventArgs e)
    {
        if (depObj is not SizeBenchFrame frame)
        {
            return;
        }

        // Verify frame reference is valid and we're not in design mode.
        if (!IsInDesignMode(frame) &&
            frame._loaded &&
            frame._updatingSourceFromNavigationService == false)
        {
            frame.Navigate((Uri)e.NewValue);
        }

        if (IsInDesignMode(frame))
        {
            if (e.NewValue != null)
            {
                frame.Content = String.Format(CultureInfo.InvariantCulture, "({0})", e.NewValue.ToString());
            }
            else
            {
                frame.Content = frame.GetType().Name;
            }
        }
    }

    #endregion

    #region CurrentPageTitle Property

    public static readonly DependencyProperty CurrentPageTitleProperty =
        DependencyProperty.Register(
            "CurrentPageTitle",
            typeof(string),
            typeof(SizeBenchFrame));

    public string CurrentPageTitle
    {
        get => (string)GetValue(CurrentPageTitleProperty);
        set => SetValue(CurrentPageTitleProperty, value);
    }

    public static readonly DependencyProperty InternalCurrentPageTitleProperty =
        DependencyProperty.Register(
            "InternalCurrentPageTitle",
            typeof(string),
            typeof(SizeBenchFrame),
            new PropertyMetadata(InternalCurrentPageTitlePropertyChanged));

#pragma warning disable IDE0051 // Remove unused private members - this is used, via the DependencyProperty in the NavigationService
    private string InternalCurrentPageTitle
#pragma warning restore IDE0051 // Remove unused private members
    {
        get => (string)GetValue(InternalCurrentPageTitleProperty);
        set => SetValue(InternalCurrentPageTitleProperty, value);
    }

    private static void InternalCurrentPageTitlePropertyChanged(DependencyObject depObj, DependencyPropertyChangedEventArgs e)
    {
        // Verify frame reference is valid and we're not in design mode.
        if (depObj is SizeBenchFrame frame &&
            !IsInDesignMode(frame) &&
            frame._loaded)
        {
            frame.CurrentPageTitle = (string)e.NewValue;
        }
    }

    #endregion

    #region CanGoBack Dependency Property

    public static readonly DependencyProperty CanGoBackProperty =
        DependencyProperty.Register(
            "CanGoBack",
            typeof(bool),
            typeof(SizeBenchFrame),
            new PropertyMetadata(OnReadOnlyPropertyChanged));

    public bool CanGoBack
    {
        get => (bool)GetValue(CanGoBackProperty);
        internal set => this.SetValueNoCallback(CanGoBackProperty, value);
    }

    #endregion

    #region CanGoForward Dependency Property

    public static readonly DependencyProperty CanGoForwardProperty =
        DependencyProperty.Register(
            "CanGoForward",
            typeof(bool),
            typeof(SizeBenchFrame),
            new PropertyMetadata(OnReadOnlyPropertyChanged));

    public bool CanGoForward
    {
        get => (bool)GetValue(CanGoForwardProperty);
        internal set => this.SetValueNoCallback(CanGoForwardProperty, value);
    }

    #endregion

    #region CurrentSource Dependency Property

    public static readonly DependencyProperty CurrentSourceProperty =
        DependencyProperty.Register(
            "CurrentSource",
            typeof(Uri),
            typeof(SizeBenchFrame),
            new PropertyMetadata(OnReadOnlyPropertyChanged));

    /// <summary>
    /// Gets the uniform resource identifier (URI) of the content that was last navigated to.
    /// </summary>
    /// <remarks>
    /// This value may be different from Source if you set Source and the
    /// navigation has not yet completed.  CurrentSource reflects the page currently
    /// in the frame at all times, even when an async loading operation is in progress.
    /// </remarks>
    public Uri CurrentSource
    {
        get => (Uri)GetValue(CurrentSourceProperty);
        internal set => this.SetValueNoCallback(CurrentSourceProperty, value);
    }

    #endregion

    #region UriMapper Dependency Property

    public static readonly DependencyProperty UriMapperProperty =
        DependencyProperty.Register(
            "UriMapper",
            typeof(UriMapperBase),
            typeof(SizeBenchFrame),
            null);

    public UriMapperBase UriMapper
    {
        get => (UriMapperBase)GetValue(UriMapperProperty);
        set => SetValue(UriMapperProperty, value);
    }

    #endregion

    #region ContentLoader Dependency Property

    public static readonly DependencyProperty ContentLoaderProperty =
        DependencyProperty.Register(
            "ContentLoader",
            typeof(INavigationContentLoader),
            typeof(SizeBenchFrame),
            new PropertyMetadata(ContentLoaderPropertyChanged));

    public INavigationContentLoader ContentLoader
    {
        get => (INavigationContentLoader)GetValue(ContentLoaderProperty);
        set => SetValue(ContentLoaderProperty, value);
    }

    private static void ContentLoaderPropertyChanged(DependencyObject depObj, DependencyPropertyChangedEventArgs e)
    {
        var frame = (SizeBenchFrame)depObj;
        var contentLoader = (INavigationContentLoader)e.NewValue;
        if (contentLoader is null)
        {
#pragma warning disable CA2208 // Instantiate argument exceptions correctly - this is the correct name of the property, DP change callbacks just have a different structure than what Code Analysis can understand
            throw new ArgumentNullException(nameof(ContentLoader));
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
        }

        if (frame.NavigationService.IsNavigating)
        {
            throw new InvalidOperationException("Cannot set ContentLoader during a load operation.");
        }
        else
        {
            frame.NavigationService.ContentLoader = contentLoader;
        }
    }

    #endregion

    #region CacheSize Dependency Property

    public static readonly DependencyProperty CacheSizeProperty =
        DependencyProperty.Register(
            "CacheSize",
            typeof(int),
            typeof(SizeBenchFrame),
            new PropertyMetadata(DefaultCacheSize, new PropertyChangedCallback(CacheSizePropertyChanged)));

    public int CacheSize
    {
        get => (int)GetValue(CacheSizeProperty);
        set => SetValue(CacheSizeProperty, value);
    }

    private static void CacheSizePropertyChanged(DependencyObject depObj, DependencyPropertyChangedEventArgs e)
    {
        var frame = (SizeBenchFrame)depObj;

        if (!frame.AreHandlersSuspended())
        {
            var newCacheSize = (int)e.NewValue;
            if (newCacheSize < 0)
            {
                frame.SetValueNoCallback(CacheSizeProperty, e.OldValue);
                throw new InvalidOperationException(
                    String.Format(CultureInfo.InvariantCulture,
                                  "{0} must be greater than or equal to zero.",
                                  "CacheSize"));
            }

            if (frame.NavigationService != null && frame.NavigationService.Cache != null)
            {
                frame.NavigationService.Cache.ChangeCacheSize(newCacheSize);
            }
        }
    }

    #endregion

    #region WindsorContainer Property

    public static readonly DependencyProperty WindsorContainerProperty =
        DependencyProperty.Register(
            "WindsorContainer",
            typeof(IWindsorContainer),
            typeof(SizeBenchFrame));

    public IWindsorContainer WindsorContainer
    {
        get => (IWindsorContainer)GetValue(WindsorContainerProperty);
        set => SetValue(WindsorContainerProperty, value);
    }

    #endregion

    #endregion

    #region Properties

    internal SizeBenchNavigationService NavigationService { get; }

    #endregion Properties

    #region Methods

    internal static bool IsInDesignMode(SizeBenchFrame navigation)
        => DesignerProperties.GetIsInDesignMode(navigation);

    private void OnQueryGoBack(object sender, CanExecuteRoutedEventArgs e)
    {
        Debug.Assert(sender == this);
        e.CanExecute = this.CanGoBack;
        e.Handled = true;
    }
    private void OnGoBack(object sender, ExecutedRoutedEventArgs e)
    {
        Debug.Assert(sender == this);
        GoBack();
        e.Handled = true;
    }

    private void OnQueryGoForward(object sender, CanExecuteRoutedEventArgs e)
    {
        Debug.Assert(sender == this);
        e.CanExecute = this.CanGoForward;
        e.Handled = true;
    }
    private void OnGoForward(object sender, ExecutedRoutedEventArgs e)
    {
        Debug.Assert(sender == this);
        GoForward();
        e.Handled = true;
    }

    private void OnQueryRefresh(object sender, CanExecuteRoutedEventArgs e)
    {
        Debug.Assert(sender == this);
        e.CanExecute = this.Content != null;
    }
    private void OnRefresh(object sender, ExecutedRoutedEventArgs e)
    {
        Debug.Assert(sender == this);
        Refresh();
        e.Handled = true;
    }


    public void StopLoading() => this.NavigationService.StopLoading();

    public void GoBack() => this.NavigationService.GoBack();

    public void GoForward() => this.NavigationService.GoForward();

    public Task<bool> Navigate(Uri source)
    {
        if (this._loaded)
        {
            return this.NavigationService.Navigate(source);
        }
        else
        {
            this._deferredNavigation = source;
            return Task.FromResult(true);
        }
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // Unhook and rehook our Next button
        if (this._nextButton != null)
        {
            this._nextButton.Click -= new RoutedEventHandler(PART_nextButton_Click);
        }

        this._nextButton = GetTemplateChild(PART_FrameNextButton) as ButtonBase;
        if (this._nextButton != null)
        {
            this._nextButton.Click += new RoutedEventHandler(PART_nextButton_Click);
        }

        if (this._previousButton != null)
        {
            this._previousButton.Click -= new RoutedEventHandler(PART_previousButton_Click);
        }

        this._previousButton = GetTemplateChild(PART_FramePreviousButton) as ButtonBase;

        if (this._previousButton != null)
        {
            this._previousButton.Click += new RoutedEventHandler(PART_previousButton_Click);
        }
    }

    public void Refresh() => this.NavigationService.Refresh();

    protected override AutomationPeer OnCreateAutomationPeer()
        => new SizeBenchFrameAutomationPeer(this);

    internal void UpdateSourceFromNavigationService(Uri newSource)
    {
        if (this.Source != newSource)
        {
            this._updatingSourceFromNavigationService = true;
            SetValue(SourceProperty, newSource);
            this._updatingSourceFromNavigationService = false;
        }
    }

    private static void OnReadOnlyPropertyChanged(DependencyObject depObj, DependencyPropertyChangedEventArgs e)
    {
        if (depObj is Frame frame && !frame.AreHandlersSuspended())
        {
            frame.SetValueNoCallback(e.Property, e.OldValue);
            throw new InvalidOperationException(
                String.Format(
                    CultureInfo.InvariantCulture,
                    "{0} cannot be set because the underlying property is read only.",
                    e.Property.ToString()));
        }
    }

    private void Frame_Loaded(object sender, RoutedEventArgs e)
    {
        this.NavigationService.InitializeJournal();

        if (this.ContentLoader is null)
        {
            this.ContentLoader = new PageResourceContentLoader();
        }

        this.NavigationService.InitializeNavigationCache();

        this._loaded = true;

        // Don't attempt to load anything at design-time
        if (!IsInDesignMode(this))
        {
            var mapper = this.UriMapper;

            if (this._deferredNavigation != null)
            {
                Navigate(this._deferredNavigation);
                this._deferredNavigation = null;
            }

            // Check if source property was set
            else if (this.Source != null)
            {
                Navigate(this.Source);
            }

            // If no Source was set, we may still be able to use UriMapper to convert this to a navigable Uri
            else if (mapper != null)
            {
                var emptyUri = new Uri(String.Empty, UriKind.Relative);
                var mappedUri = mapper.MapUri(emptyUri);
                if (mappedUri != null && !String.IsNullOrEmpty(mappedUri.OriginalString))
                {
                    Navigate(emptyUri);
                }
            }
        }
        else
        {
            if (this.Source != null)
            {
                this.Content = String.Format(CultureInfo.InvariantCulture, "({0})", this.Source.ToString());
            }
            else
            {
                this.Content = nameof(Frame);
            }
        }
    }

    private void PART_nextButton_Click(object sender, RoutedEventArgs e) => GoForward();

    private void PART_previousButton_Click(object sender, RoutedEventArgs e) => GoBack();

    #endregion Methods
}
