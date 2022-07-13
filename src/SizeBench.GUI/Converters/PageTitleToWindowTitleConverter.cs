using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SizeBench.GUI.Converters;

public sealed class PageTitleToWindowTitleConverter : IMultiValueConverter
{
    public static PageTitleToWindowTitleConverter Instance { get; } = new PageTitleToWindowTitleConverter();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values?.Length > 2)
        {
            throw new ArgumentException("This converter expects at most two inputs - the page title and the binary path");
        }

        if (values is null || values.Length < 1 || values[0] is null || values[0] == DependencyProperty.UnsetValue)
        {
            return "SizeBench";
        }

        var selectedBinaryPath = values.Length == 2 && values[1] != DependencyProperty.UnsetValue ? values[1] as string : String.Empty;

#pragma warning disable CA1508 // For some reason code analysis thinks that values[0] is always a string, but I can't understand how, so I'm disabling the warning and keeping the type check
        if (values[0] is not string pageTitle)
        {
            throw new ArgumentException("values[0] must be a string");
        }
#pragma warning restore CA1508

        if (selectedBinaryPath is null)
        {
            throw new ArgumentException("values[1] must be a string");
        }

        if (selectedBinaryPath.Length > 0)
        {
            return $"SizeBench - {pageTitle} ({selectedBinaryPath})";
        }
        else
        {
            return $"SizeBench - {pageTitle}";
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
