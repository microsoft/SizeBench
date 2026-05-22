using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using DiffPlex.Wpf.Controls;

#nullable disable // Disabling nullability for IValueConverters, since WPF doesn't define them correctly yet (they should return object? not object)

namespace SizeBench.GUI.Converters;

public sealed class TwoStringsToDiffViewerUIConverter : IMultiValueConverter
{
    public static TwoStringsToDiffViewerUIConverter Instance { get; } = new TwoStringsToDiffViewerUIConverter();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (targetType != typeof(object))
        {
            throw new ArgumentException("targetType must be object - this is really meant to go into ContentPresenter.Content");
        }

        if (values?.Length > 3)
        {
            throw new ArgumentException("This converter expects two or three inputs - the two strings to diff, and optionally a zoom percent.");
        }

        if (values is null || values.Length < 2 || values[0] is null || values[0] == DependencyProperty.UnsetValue || values[1] is null || values[1] == DependencyProperty.UnsetValue)
        {
            return null;
        }

#pragma warning disable CA1508 // For some reason code analysis thinks that values[0] and values[1] are always strings, but I can't understand how, so I'm disabling the warning and keeping the type checks
        if (values[0] is not string leftString)
        {
            throw new ArgumentException("values[0] must be a string");
        }

        if (values[1] is not string rightString)
        {
            throw new ArgumentException("values[1] must be a string");
        }
#pragma warning restore CA1508

        var zoomPercent = 100;
        if (values.Length == 3 && values[2] != DependencyProperty.UnsetValue && values[2] != null)
        {
            if (values[2] is not int requestedZoomPercent)
            {
                throw new ArgumentException("values[2] must be an int zoom percent");
            }

            zoomPercent = requestedZoomPercent;
        }

        //TODO: Consider showing the function names in the headers (OldTextHeader and NewTextHeader) - but piping that through
        //      this IMultiValueConverter seems tedious so skipping for now.  In the current UI, the function names are shown in
        //      the ComboBoxes above anyway.
        var diffViewer = new DiffViewer()
        {
            OldText = leftString,
            NewText = rightString,
            InsertedBackground = new SolidColorBrush(Color.FromRgb(255, 255, 187)),
            DeletedBackground = new SolidColorBrush(Color.FromRgb(255, 168, 168)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 16.0 * zoomPercent / 100.0
        };
        diffViewer.ShowInline();

        return diffViewer;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
