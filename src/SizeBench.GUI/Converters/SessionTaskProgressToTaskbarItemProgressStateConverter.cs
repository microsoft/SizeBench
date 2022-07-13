using System.Globalization;
using System.Windows.Data;
using System.Windows.Shell;
using SizeBench.AnalysisEngine;

namespace SizeBench.GUI.Converters;

public sealed class SessionTaskProgressToTaskbarItemProgressStateConverter : IValueConverter
{
    public static SessionTaskProgressToTaskbarItemProgressStateConverter Instance { get; } = new SessionTaskProgressToTaskbarItemProgressStateConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not SessionTaskProgress progress)
        {
            throw new ArgumentException("Must be a SessionTaskProgress", nameof(value));
        }

        if (progress.IsProgressIndeterminate)
        {
            return TaskbarItemProgressState.Indeterminate;
        }
        else
        {
            return TaskbarItemProgressState.Normal;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
