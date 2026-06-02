using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

#nullable disable // Disabling nullability for IValueConverters, since WPF doesn't define them correctly yet (they should return object? not object)

namespace SizeBench.GUI.Converters;

public sealed class TwoStringsToDiffViewerUIConverter : IMultiValueConverter
{
    private static readonly Brush InsertedBackground = new SolidColorBrush(Color.FromRgb(255, 255, 187));
    private static readonly Brush DeletedBackground = new SolidColorBrush(Color.FromRgb(255, 168, 168));
    private static readonly Brush ImaginaryBackground = new SolidColorBrush(Color.FromRgb(230, 230, 230));

    static TwoStringsToDiffViewerUIConverter()
    {
        InsertedBackground.Freeze();
        DeletedBackground.Freeze();
        ImaginaryBackground.Freeze();
    }

    public static TwoStringsToDiffViewerUIConverter Instance { get; } = new TwoStringsToDiffViewerUIConverter();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (targetType != typeof(object))
        {
            throw new ArgumentException("targetType must be object - this is really meant to go into ContentPresenter.Content");
        }

        if (values?.Length > 2)
        {
            throw new ArgumentException("This converter expects exactly two inputs - the two strings to diff");
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

        var diff = InlineDiffBuilder.Diff(leftString, rightString);

        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 16,
            PagePadding = new Thickness(0),
        };

        foreach (var line in diff.Lines)
        {
            var prefix = line.Type switch
            {
                ChangeType.Inserted => "+ ",
                ChangeType.Deleted => "- ",
                ChangeType.Modified => "~ ",
                ChangeType.Imaginary => "  ",
                _ => "  "
            };

            var background = line.Type switch
            {
                ChangeType.Inserted => InsertedBackground,
                ChangeType.Deleted => DeletedBackground,
                ChangeType.Imaginary => ImaginaryBackground,
                _ => Brushes.Transparent
            };

            var paragraph = new Paragraph(new Run(prefix + (line.Text ?? string.Empty)))
            {
                Background = background,
                Margin = new Thickness(0),
                Padding = new Thickness(2, 0, 2, 0),
            };

            document.Blocks.Add(paragraph);
        }

        return new FlowDocumentScrollViewer
        {
            Document = document,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            IsToolBarVisible = false,
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
