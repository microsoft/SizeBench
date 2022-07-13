using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SizeBench.GUI.Controls.TypeLayoutTreeView;

public sealed class IsAlignmentMemberToFontWeightConverter : IValueConverter
{
    public static IsAlignmentMemberToFontWeightConverter Instance { get; } = new IsAlignmentMemberToFontWeightConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not bool)
        {
            throw new ArgumentException("value should be a bool");
        }

        if (targetType != typeof(FontWeight))
        {
            throw new ArgumentException("targetType should be FontWeight");
        }

        var isAlignmentMember = (bool)value;
        if (isAlignmentMember)
        {
            return FontWeights.Bold;
        }
        else
        {
            return FontWeights.Normal;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
