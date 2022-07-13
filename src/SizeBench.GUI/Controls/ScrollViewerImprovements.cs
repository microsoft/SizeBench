using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SizeBench.GUI.Controls;

// Very hard to unit test UI stuff like this
[ExcludeFromCodeCoverage]
public static class ScrollViewerImprovements
{
    public static bool GetEnableScrollChaining(DependencyObject obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        return (bool)obj.GetValue(EnableScrollChainingProperty);
    }

    public static void SetEnableScrollChaining(DependencyObject obj, bool value) => obj?.SetValue(EnableScrollChainingProperty, value);

    public static readonly DependencyProperty EnableScrollChainingProperty =
        DependencyProperty.RegisterAttached("EnableScrollChaining", typeof(bool), typeof(ScrollViewerImprovements),
                                            new FrameworkPropertyMetadata(false, OnEnableScrollChainingPropertyChanged));

    private static void OnEnableScrollChainingPropertyChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not ScrollViewer viewer)
        {
            throw new ArgumentException("The dependency property can only be attached to a ScrollViewer", nameof(sender));
        }

        if ((bool)e.NewValue == true)
        {
            viewer.PreviewMouseWheel += HandlePreviewMouseWheel;
        }
        else if ((bool)e.NewValue == false)
        {
            viewer.PreviewMouseWheel -= HandlePreviewMouseWheel;
        }
    }

    [ThreadStatic]
    private static readonly List<MouseWheelEventArgs> _reentrantList = new List<MouseWheelEventArgs>();
    private static void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!e.Handled && sender is ScrollViewer scrollControl && !_reentrantList.Contains(e))
        {
            var previewEventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.PreviewMouseWheelEvent,
                Source = scrollControl
            };

            UIElement? originalSource = null;
            if (e.OriginalSource is UIElement argsOriginalSourceAsUIE)
            {
                originalSource = argsOriginalSourceAsUIE;
            }
            else if (e.OriginalSource is FrameworkContentElement argsOriginalSourceAsFCE)
            {
                var parent = argsOriginalSourceAsFCE.Parent;
                while (parent is not UIElement)
                {
                    // For reasons that I cannot fathom, VisualTreeHelper cannot find parents of FrameworkContentElements - it can only find parents
                    // for Visual and Visual3D things.  So we need to special-case FCE to walk up parent chains of Runs/Hyperlinks/etc.
                    if (parent is FrameworkContentElement parentAsFCE)
                    {
                        parent = parentAsFCE.Parent;
                    }
                    else
                    {
                        parent = VisualTreeHelper.GetParent(parent);
                    }
                }
                originalSource = parent as UIElement;
            }

            if (originalSource is null)
            {
                throw new InvalidOperationException("Somehow we were unable to determine the correct original source for scroll chaining.");
            }

            _reentrantList.Add(previewEventArg);
            try
            {
                originalSource.RaiseEvent(previewEventArg);
            }
            finally
            {
                _reentrantList.Remove(previewEventArg);
            }

            // at this point if no one else handled the event in our children, we do our job
            if (!previewEventArg.Handled && ((e.Delta > 0 && scrollControl.VerticalOffset == 0)
                || (e.Delta <= 0 && scrollControl.VerticalOffset >= scrollControl.ExtentHeight - scrollControl.ViewportHeight)))
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };
                var parent = (UIElement)((FrameworkElement)sender).Parent;
                parent.RaiseEvent(eventArg);
            }
        }
    }
}
