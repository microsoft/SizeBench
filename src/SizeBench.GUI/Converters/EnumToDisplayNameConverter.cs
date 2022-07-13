using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Windows.Data;

namespace SizeBench.GUI.Converters;

public sealed class EnumToDisplayNameConverter : IValueConverter
{
    // TODO: this code is almost entirely duplicated with SKUCrawler since there's not an obvious place to share code between the GUI and SKUCrawler now when it doesn't belong
    //       in the analysis engine (and I don't think this does)
    private static class EnumHelper
    {
        public static string GetDisplayValue(Enum value)
        {
            var fieldInfo = value.GetType().GetField(value.ToString());

            if (fieldInfo!.GetCustomAttributes(typeof(DisplayAttribute), false) is not DisplayAttribute[] displayAttributes ||
                displayAttributes.Length == 0)
            {
                return value.ToString();
            }

            var displayAttribute = displayAttributes[0];
            return displayAttribute.Name ?? value.ToString();
        }
    }

    public static EnumToDisplayNameConverter Instance { get; } = new EnumToDisplayNameConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Enum enumValue)
        {
            throw new ArgumentException("must be enum", nameof(value));
        }

        return EnumHelper.GetDisplayValue(enumValue);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
