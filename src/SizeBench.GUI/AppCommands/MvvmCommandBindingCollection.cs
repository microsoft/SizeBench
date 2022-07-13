using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;

namespace SizeBench.GUI.Commands;

// Taken from https://gist.github.com/vbfox/1445370
/// <summary>
/// Set an element <see cref="UIElement.CommandBindings"/> with a syntax allowing to specify an
/// <see cref="ICommand"/> instance using bindings if required.
/// </summary>
/// <example>
/// &lt;u:Mvvm.CommandBindings&gt;
///     &lt;u:MvvmCommandBindingCollection&gt;
///         &lt;u:MvvmCommandBinding Command="cmd:RoutedCommands.SomeCommand"
///                                  Target="{Binding CommandInViewModel}" /&gt;
///     &lt;/u:MvvmCommandBindingCollection&gt;
/// &lt;/u:Mvvm.CommandBindings&gt;
/// </example>
[ExcludeFromCodeCoverage]
[ContentProperty("Commands")]
internal class MvvmCommandBindingCollection : Freezable
{
    // Normally the inheritance context only goes to the logical and visual tree. But there are some additional
    // "Pointers" that exists to simplify XAML programming. The one that we use there is that the context is
    // propagated when a hierarchy of Freezable is inside a FrameworkElement.
    //
    // It is acheived by the facts that :
    //  * This class is Freezable
    //  * The collection property is a dependency property
    //  * The collection is Freezable (FreezableCollection<T> is an Animatable that is a Freezable)
    //  * The objects inside the collection are instances of Freezable

    private static readonly DependencyPropertyKey commandsPropertyReadWrite =
        DependencyProperty.RegisterReadOnly("Commands", typeof(FreezableCollection<MvvmCommandBinding>),
        typeof(MvvmCommandBindingCollection), null);

    public static readonly DependencyProperty CommandsProperty = commandsPropertyReadWrite.DependencyProperty;

    public FreezableCollection<MvvmCommandBinding> Commands
    {
        get { return (FreezableCollection<MvvmCommandBinding>)GetValue(CommandsProperty); ; }
        private set => SetValue(commandsPropertyReadWrite, value);
    }

    private UIElement? uiElement;

    public MvvmCommandBindingCollection()
    {
        this.Commands = new FreezableCollection<MvvmCommandBinding>();
        ((INotifyCollectionChanged)this.Commands).CollectionChanged += CommandsChanged;
    }

    private void CommandsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (this.uiElement is null)
        {
            return;
        }

        if (e.NewItems != null && e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Replace)
        {
            foreach (MvvmCommandBinding? command in e.NewItems)
            {
                command!.AttachTo(this.uiElement);
            }
        }

        if (e.OldItems != null && e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace)
        {
            foreach (MvvmCommandBinding? command in e.OldItems)
            {
                command!.DetachFrom(this.uiElement);
            }
        }
    }

    internal void DetachFrom(UIElement uiDependencyObject)
    {
        ArgumentNullException.ThrowIfNull(uiDependencyObject);

        WritePreamble();

        if (uiDependencyObject != this.uiElement)
        {
            return;
        }

        Detach();
    }

    private void Detach()
    {
        if (this.uiElement is null)
        {
            return;
        }

        foreach (var command in this.Commands)
        {
            command.DetachFrom(this.uiElement);
        }

        this.uiElement = null;
    }

    internal void AttachTo(UIElement uiDependencyObject)
    {
        ArgumentNullException.ThrowIfNull(uiDependencyObject);

        WritePreamble();

        if (this.uiElement != null)
        {
            Detach();
        }

        this.uiElement = uiDependencyObject;

        foreach (var command in this.Commands)
        {
            command.AttachTo(this.uiElement);
        }
    }

    protected override Freezable CreateInstanceCore()
        => new MvvmCommandBindingCollection();
}
