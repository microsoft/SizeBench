using System.Globalization;
using System.Windows.Data;
using SizeBench.AnalysisEngine;

namespace SizeBench.GUI.Converters;

public sealed class CompilandDiffAndCOFFGroupDiffToContributionVirtualSizeConverter : IMultiValueConverter
{
    public static CompilandDiffAndCOFFGroupDiffToContributionVirtualSizeConverter Instance { get; } = new CompilandDiffAndCOFFGroupDiffToContributionVirtualSizeConverter();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values?.Length != 2)
        {
            throw new ArgumentException("This converter expects at most two inputs - the compiland diff and the COFF Group diff");
        }

        if (values[0] is not CompilandDiff compiland)
        {
            throw new ArgumentException("values[0] must be a CompilandDiff");
        }

        if (values[1] is not COFFGroupDiff coffGroup)
        {
            throw new ArgumentException("values[1] must be a COFFGroupDiff");
        }

        if (compiland.COFFGroupContributionDiffs.ContainsKey(coffGroup))
        {
            return SizeToFriendlySizeConverter.Instance.Convert(compiland.COFFGroupContributionDiffs[coffGroup].VirtualSizeDiff, targetType, parameter, culture);
        }
        else
        {
            return "0 bytes";
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
