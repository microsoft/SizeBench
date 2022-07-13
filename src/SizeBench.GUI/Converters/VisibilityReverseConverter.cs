using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SizeBench.GUI.Converters;

public sealed class VisibilityReverseConverter : IValueConverter
{
    public static VisibilityReverseConverter Instance { get; } = new VisibilityReverseConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (Visibility)value == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Convert(value, targetType, parameter, culture);
}
