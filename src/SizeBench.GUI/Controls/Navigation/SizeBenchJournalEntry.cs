using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace SizeBench.GUI.Controls.Navigation;

// This type is excluded from code coverage because it's basically a big copy/paste from the Silverlight SDK and in Silverlight
// this was extensively tested.  See details at the top of the file for SizeBenchFrame.
[ExcludeFromCodeCoverage]
internal sealed class SizeBenchJournalEntry : DependencyObject
{
    #region Fields

    private Uri _source;

    #endregion Fields

    #region Constructors

    public SizeBenchJournalEntry(string name, Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        this.Name = name;
        this._source = uri;
    }

    #endregion Constructors

    #region Name Attached Property

    public static readonly DependencyProperty NameProperty =
        DependencyProperty.RegisterAttached(
            "Name",
            typeof(string),
            typeof(SizeBenchJournalEntry));

    public string Name
    {
        get => (string)GetValue(NameProperty);
        set => SetValue(NameProperty, value);
    }

    public static string GetName(DependencyObject depObj)
    {
        ArgumentNullException.ThrowIfNull(depObj);
        return (string)depObj.GetValue(NameProperty);
    }

    public static void SetName(DependencyObject depObj, string name)
    {
        ArgumentNullException.ThrowIfNull(depObj);
        depObj.SetValue(NameProperty, name);
    }

    #endregion Name Attached Property

    #region NavigationContext Attached Property

    public static readonly DependencyProperty NavigationContextProperty = DependencyProperty.RegisterAttached("NavigationContext", typeof(NavigationContext), typeof(SizeBenchJournalEntry), null);

    public NavigationContext NavigationContext
    {
        get => (NavigationContext)GetValue(NavigationContextProperty);
        set => SetValue(NavigationContextProperty, value);
    }

    public static NavigationContext GetNavigationContext(DependencyObject obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        return (NavigationContext)obj.GetValue(NavigationContextProperty);
    }

    public static void SetNavigationContext(DependencyObject obj, NavigationContext navigationContext)
    {
        ArgumentNullException.ThrowIfNull(obj);
        obj.SetValue(NavigationContextProperty, navigationContext);
    }

    #endregion NavigationContext Attached Property

    #region Properties

    public Uri Source
    {
        get => this._source;

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            this._source = value;
        }
    }

    #endregion Properties
}
