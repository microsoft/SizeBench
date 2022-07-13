using System.Windows;
using System.Windows.Controls;

namespace SizeBench.GUI;

public sealed class AppWideDialogTemplateSelector : DataTemplateSelector
{
    public static AppWideDialogTemplateSelector Instance { get; } = new AppWideDialogTemplateSelector();

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (container is FrameworkElement element && item != null)
        {
            if (item is AppWideModalMessageDialogViewModel or AppWideModalProgressOnlyDialogViewModel)
            {
                return element.FindResource("MessageTemplate") as DataTemplate;
            }
            else if (item is AppWideModalErrorDialogViewModel)
            {
                return element.FindResource("ErrorTemplate") as DataTemplate;
            }
        }

        return null;
    }
}
