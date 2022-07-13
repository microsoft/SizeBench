using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SizeBench.GUI.Converters;

public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public static BooleanToVisibilityConverter Instance { get; } = new BooleanToVisibilityConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not bool)
        {
            throw new ArgumentException("must be bool", nameof(value));
        }

        if (parameter != null && parameter is string && String.Equals(parameter as string, "Reverse", StringComparison.OrdinalIgnoreCase))
        {
            return ((bool)value) ? Visibility.Collapsed : Visibility.Visible;
        }
        else
        {
            return ((bool)value) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Visibility)
        {
            throw new ArgumentException("must be Visibility", nameof(value));
        }

        if (targetType != typeof(bool))
        {
            throw new ArgumentException("must target bool", nameof(targetType));
        }

        return ((Visibility)value) == Visibility.Visible;
    }
}
