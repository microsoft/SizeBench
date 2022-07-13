using System.Globalization;
using System.Windows.Data;

namespace SizeBench.GUI.Converters;

public sealed class SizeToFriendlySizeConverter : IValueConverter
{
    public static SizeToFriendlySizeConverter Instance { get; } = new SizeToFriendlySizeConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var size = System.Convert.ToInt64(value, CultureInfo.InvariantCulture);

        if (Math.Abs(size) >= 1024 * 1024 * 1024)
        {
            return $"{(size / (1024.0f * 1024.0f * 1024.0f)):N1} GB";
        }
        else if (Math.Abs(size) >= 1024 * 1024)
        {
            return $"{(size / (1024.0f * 1024.0f)):N1} MB";
        }
        else if (Math.Abs(size) >= 1024)
        {
            return $"{(size / 1024.0f):N1} KB";
        }
        else
        {
            return $"{size} bytes";
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
