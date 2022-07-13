using System.Globalization;
using System.Windows.Data;
using SizeBench.AnalysisEngine;

namespace SizeBench.GUI.Converters;

public sealed class LibAndCOFFGroupToContributionSizeConverter : IMultiValueConverter
{
    public static LibAndCOFFGroupToContributionSizeConverter Instance { get; } = new LibAndCOFFGroupToContributionSizeConverter();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values?.Length != 2)
        {
            throw new ArgumentException("This converter expects at most two inputs - the lib and the COFF Group");
        }

        if (values[0] is not Library lib)
        {
            throw new ArgumentException("values[0] must be a Lib");
        }

        if (values[1] is not COFFGroup coffGroup)
        {
            throw new ArgumentException("values[1] must be a COFFGroup");
        }

        if (lib.COFFGroupContributions.ContainsKey(coffGroup))
        {
            return SizeToFriendlySizeConverter.Instance.Convert(lib.COFFGroupContributions[coffGroup].Size, targetType, parameter, culture);
        }
        else
        {
            return "0 bytes";
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
