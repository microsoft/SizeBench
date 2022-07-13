using System.Windows;
using System.Windows.Data;
using Microsoft.Xaml.Behaviors;

namespace SizeBench.GUI.Behaviors;

public sealed class CollectionViewSourceFilterBehavior : Behavior<CollectionViewSource>
{
    public Func<object, bool> Filter
    {
        get => (Func<object, bool>)GetValue(FilterProperty);
        set => SetValue(FilterProperty, value);
    }

    public static readonly DependencyProperty FilterProperty =
           DependencyProperty.RegisterAttached("Filter",
           typeof(Func<object, bool>),
           typeof(CollectionViewSourceFilterBehavior));

    protected override void OnAttached()
    {
        base.OnAttached();
        this.AssociatedObject.Filter += AssociatedObject_Filter;
    }

    protected override void OnDetaching()
    {
        this.AssociatedObject.Filter -= AssociatedObject_Filter;
        base.OnDetaching();
    }

    private void AssociatedObject_Filter(object sender, FilterEventArgs e)
        => e.Accepted = this.Filter(e.Item);
}
