using System.Windows;

namespace SizeBench.GUI.Core;

// This type is a way to help things not in the logical or visual tree to be able to attach
// bindings.  An example is DataGrid columns.

// The code is directly lifted from this StackOverflow post:
// http://stackoverflow.com/questions/15494226/cannot-find-source-for-binding-with-reference-relativesource-findancestor
public class BindingProxy : Freezable
{
    protected override Freezable CreateInstanceCore() => new BindingProxy();

    public object Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    // Using a DependencyProperty as the backing store for Data.
    // This enables animation, styling, binding, etc...
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register("Data", typeof(object),
        typeof(BindingProxy), new UIPropertyMetadata(null));
}
