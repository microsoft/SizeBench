using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SizeBench.GUI.Controls.TypeLayoutTreeView;

public class SizeDiffToGoodOrBadForegroundConverter : IValueConverter
{
    private static readonly Brush _goodBrush = new SolidColorBrush(Colors.Green);
    private static readonly Brush _badBrush = new SolidColorBrush(Colors.Red);
    private static readonly Brush _defaultBrush = new SolidColorBrush(Colors.Black);

    public static SizeDiffToGoodOrBadForegroundConverter Instance { get; } = new SizeDiffToGoodOrBadForegroundConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TypeLayoutItemDiffViewModel and not TypeLayoutItemDiffViewModel.MemberDiffViewModel)
        {
            throw new ArgumentException("value should be a TypeLayoutItemDiffViewModel or a MemberDiffViewModel");
        }

        if (targetType != typeof(Brush))
        {
            throw new ArgumentException("targetType should be Brush");
        }

        if (value is TypeLayoutItemDiffViewModel itemDiffVM)
        {
            if (itemDiffVM.TypeLayoutItemDiff is null)
            {
                return _defaultBrush;
            }

            if (itemDiffVM.TypeLayoutItemDiff.AfterTypeLayout is null)
            {
                return _goodBrush;
            }

            if (itemDiffVM.TypeLayoutItemDiff.BeforeTypeLayout is null)
            {
                return _badBrush;
            }

            if (itemDiffVM.TypeLayoutItemDiff.InstanceSizeDiff < 0)
            {
                return _goodBrush;
            }
            else if (itemDiffVM.TypeLayoutItemDiff.InstanceSizeDiff > 0)
            {
                return _badBrush;
            }
        }
        else if (value is TypeLayoutItemDiffViewModel.MemberDiffViewModel memDiffVM)
        {
            if (memDiffVM.Member.AfterMember is null)
            {
                return _goodBrush;
            }

            if (memDiffVM.Member.BeforeMember is null)
            {
                return _badBrush;
            }

            if (memDiffVM.Member.SizeDiff < 0)
            {
                return _goodBrush;
            }
            else if (memDiffVM.Member.SizeDiff > 0)
            {
                return _badBrush;
            }
        }

        return _defaultBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
