using System.Globalization;
using System.Windows.Data;
using SizeBench.AnalysisEngine;

namespace SizeBench.GUI.Converters;

public sealed class CompilandAndCOFFGroupToContributionVirtualSizeConverter : IMultiValueConverter
{
    public static CompilandAndCOFFGroupToContributionVirtualSizeConverter Instance { get; } = new CompilandAndCOFFGroupToContributionVirtualSizeConverter();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values?.Length != 2)
        {
            throw new ArgumentException("This converter expects at most two inputs - the compiland and the COFF Group");
        }

        if (values[0] is not Compiland compiland)
        {
            throw new ArgumentException("values[0] must be a Compiland");
        }

        if (values[1] is not COFFGroup coffGroup)
        {
            throw new ArgumentException("values[1] must be a COFFGroup");
        }

        if (compiland.COFFGroupContributions.ContainsKey(coffGroup))
        {
            return SizeToFriendlySizeConverter.Instance.Convert(compiland.COFFGroupContributions[coffGroup].VirtualSize, targetType, parameter, culture);
        }
        else
        {
            return "0 bytes";
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
