using System.Globalization;
using System.Windows.Data;
using SizeBench.AnalysisEngine;

namespace SizeBench.GUI.Converters;

public sealed class SessionTaskProgressToProgressPercentConverter : IValueConverter
{
    public static SessionTaskProgressToProgressPercentConverter Instance { get; } = new SessionTaskProgressToProgressPercentConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not SessionTaskProgress progress)
        {
            throw new ArgumentException("Must be a SessionTaskProgress", nameof(value));
        }

        if (progress.ItemsTotal == 0)
        {
            return 0.0d;
        }

        return (progress.ItemsComplete / (double)progress.ItemsTotal);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
