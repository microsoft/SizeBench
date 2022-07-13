using System.Globalization;
using System.Windows.Data;

namespace SizeBench.GUI.Controls.TypeLayoutTreeView;

public sealed class FullTypeNameToToolTipTypeNameConverter : IValueConverter
{
    public static FullTypeNameToToolTipTypeNameConverter Instance { get; } = new FullTypeNameToToolTipTypeNameConverter();

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return null;
        }

        if (value is not string)
        {
            throw new ArgumentException("value must be a string");
        }

        var str = (string)value;
        if (str.Length > 45 || str.Contains("::", StringComparison.Ordinal))
        {
            return str;
        }
        else
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
