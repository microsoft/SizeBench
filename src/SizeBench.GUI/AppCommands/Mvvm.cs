using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace SizeBench.GUI.Commands;

// Taken from https://gist.github.com/vbfox/1445370
[ExcludeFromCodeCoverage]
internal static class Mvvm
{
    public static readonly DependencyProperty CommandBindingsProperty = DependencyProperty.RegisterAttached(
        "CommandBindings", typeof(MvvmCommandBindingCollection), typeof(Mvvm),
        new PropertyMetadata(null, OnCommandBindingsChanged));

    [AttachedPropertyBrowsableForType(typeof(UIElement))]
    public static MvvmCommandBindingCollection GetCommandBindings(UIElement target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return (MvvmCommandBindingCollection)target.GetValue(CommandBindingsProperty);
    }

    public static void SetCommandBindings(UIElement target, MvvmCommandBindingCollection value)
    {
        ArgumentNullException.ThrowIfNull(target);

        target.SetValue(CommandBindingsProperty, value);
    }

    private static void OnCommandBindingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement uiElement)
        {
            return;
        }

        if (e.OldValue is MvvmCommandBindingCollection oldValue)
        {
            oldValue.DetachFrom(uiElement);
        }

        if (e.NewValue is MvvmCommandBindingCollection newValue)
        {
            newValue.AttachTo(uiElement);
        }
    }
}
