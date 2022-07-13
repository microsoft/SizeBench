using System.Windows;
using System.Windows.Controls;

namespace SizeBench.GUI.Core;

public sealed class TabWideDialogTemplateSelector : DataTemplateSelector
{
    public static TabWideDialogTemplateSelector Instance { get; } = new TabWideDialogTemplateSelector();

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (container is FrameworkElement element && item != null)
        {
            if (item is TabBase.SingleBinaryProgressDialogViewModel or TabBase.GenericProgressDialogViewModel)
            {
                return element.FindResource("SingleBinaryProgressDialogTemplate") as DataTemplate;
            }
            else if (item is TabBase.BinaryDiffProgressDialogViewModel)
            {
                return element.FindResource("DiffProgressDialogTemplate") as DataTemplate;
            }
        }

        return null;
    }
}
