using System.Globalization;
using System.Windows.Data;
using SizeBench.AnalysisEngine;

namespace SizeBench.GUI.Converters;

public sealed class LibDiffAndCOFFGroupDiffToContributionSizeConverter : IMultiValueConverter
{
    public static LibDiffAndCOFFGroupDiffToContributionSizeConverter Instance { get; } = new LibDiffAndCOFFGroupDiffToContributionSizeConverter();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values?.Length != 2)
        {
            throw new ArgumentException("This converter expects at most two inputs - the lib diff and the COFF Group diff");
        }

        if (values[0] is not LibDiff lib)
        {
            throw new ArgumentException("values[0] must be a LibDiff");
        }

        if (values[1] is not COFFGroupDiff coffGroup)
        {
            throw new ArgumentException("values[1] must be a COFFGroupDiff");
        }

        if (lib.COFFGroupContributionDiffs.ContainsKey(coffGroup))
        {
            return SizeToFriendlySizeConverter.Instance.Convert(lib.COFFGroupContributionDiffs[coffGroup].SizeDiff, targetType, parameter, culture);
        }
        else
        {
            return "0 bytes";
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
