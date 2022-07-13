using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SizeBench.GUI.Controls.TypeLayoutTreeView;

public class TypeOrMemberLayoutToTextDecorationsConverter : IValueConverter
{
    public static TypeOrMemberLayoutToTextDecorationsConverter Instance { get; } = new TypeOrMemberLayoutToTextDecorationsConverter();

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TypeLayoutItemDiffViewModel and not TypeLayoutItemDiffViewModel.MemberDiffViewModel)
        {
            throw new ArgumentException("value should be a TypeLayoutItemDiffViewModel or a MemberDiffViewModel");
        }

        if (targetType != typeof(TextDecorationCollection))
        {
            throw new ArgumentException("targetType should be TextDecorationCollection");
        }

        if (value is TypeLayoutItemDiffViewModel typeLayoutVM)
        {
            if (typeLayoutVM.TypeLayoutItemDiff is null)
            {
                return null;
            }

            if (typeLayoutVM.TypeLayoutItemDiff.AfterTypeLayout is null)
            {
                return TextDecorations.Strikethrough;
            }
        }
        else if (value is TypeLayoutItemDiffViewModel.MemberDiffViewModel memberVM)
        {
            if (memberVM.Member.AfterMember is null)
            {
                return TextDecorations.Strikethrough;
            }
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
